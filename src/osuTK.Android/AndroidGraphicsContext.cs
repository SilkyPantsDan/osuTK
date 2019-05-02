#region --- License ---
/* Licensed under the MIT/X11 license.
 * Copyright (c) 2011 Xamarin, Inc.
 * Copyright 2013 Xamarin Inc
 * This notice may not be removed from any source distribution.
 * See license.txt for licensing detailed licensing details.
 */
#endregion

using System;

using Android.Util;
using Android.Views;
using Android.Runtime;
using Javax.Microedition.Khronos.Egl;

using osuTK.Platform;
using osuTK.Graphics;
using osuTK.Graphics.ES11;

using GL11 = osuTK.Graphics.ES11.GL;
using GL20 = osuTK.Graphics.ES20.GL;

namespace osuTK.Platform.Android {

	public class AndroidGraphicsContext : IGraphicsContext, IGraphicsContextInternal, IDisposable
	{
		IEGL10 egl;
		AndroidWindow window;
		bool disposed;
		EGLSurface surface = null;

		public EGLContext EGLContext { get; private set; }

		public EGLConfig EGLConfig {
			get {
				if (Mode != null)
					return Mode.Config;
				return null;
			}
		}

		public bool PBufferSupported {
			get {
				if (Mode != null)
					return Mode.PBufferSupported;
				return false;
			}
		}

#if OPENTK_0
		internal static AndroidGraphicsContext CreateGraphicsContext (GraphicsMode mode, IWindowInfo window,
			IGraphicsContext sharedContext, GLContextVersion glVersion, GraphicsContextFlags flags)
#else
		internal static AndroidGraphicsContext CreateGraphicsContext (GraphicsMode mode, IWindowInfo window,
			IGraphicsContext sharedContext, GLVersion glVersion, GraphicsContextFlags flags)
#endif
		{
			return new AndroidGraphicsContext(mode, window, sharedContext, glVersion, flags);
		}

		internal static AndroidGraphicsContext CreateGraphicsContext (GraphicsMode mode, IWindowInfo window,
			IGraphicsContext sharedContext, int major, int minor, GraphicsContextFlags flags)
		{
			if (major < 1 || major > 3)
				throw new ArgumentException (string.Format("Unsupported GLES version {0}.{1}.", major, minor));

			return new AndroidGraphicsContext(mode, window, sharedContext, major, minor, flags);
		}

		internal AndroidGraphicsContext(ContextHandle h)
		{
			throw new NotImplementedException ();
		}

		public AndroidGraphicsContext (GraphicsMode mode, IWindowInfo window, IGraphicsContext sharedContext,
										int major, int minor, GraphicsContextFlags flags)
		{
			if (major < 1 || major > 3)
				throw new ArgumentException (string.Format("Unsupported GLES version {0}.{1}.", major, minor));

			Init (mode, window, sharedContext, major, minor, flags);
		}

#if OPENTK_0
		public AndroidGraphicsContext (GraphicsMode mode, IWindowInfo window, IGraphicsContext sharedContext,
								GLContextVersion glesVersion, GraphicsContextFlags flags)
#else
		public AndroidGraphicsContext (GraphicsMode mode, IWindowInfo window, IGraphicsContext sharedContext,
								GLVersion glesVersion, GraphicsContextFlags flags)
#endif
		{
			int major = (int)glesVersion;
			int minor = 0;
#if OPENTK_0
			major--;
#endif
			// ES 3.1
			if (major == 4) {
				major = 3;
				minor = 1;
			}

			Init (mode, window, sharedContext, major, minor, flags);
		}

		void Init (GraphicsMode mode, IWindowInfo win, IGraphicsContext sharedContext,
										int major, int minor, GraphicsContextFlags flags)
		{
			window = win as AndroidWindow;
			if (window == null)
				throw new ArgumentException ("win");

			AndroidGraphicsContext shared = sharedContext as AndroidGraphicsContext;

			egl = EGLContext.EGL.JavaCast<IEGL10> ();

			window.InitializeDisplay ();

			if (mode == null)
				mode = new GraphicsMode ();

			if (mode is AndroidGraphicsMode) {
				GraphicsMode = mode;
			} else {
				GraphicsMode = new AndroidGraphicsMode (window.Display, major, mode);
			}

			if (shared != null && !PBufferSupported)
				throw new EglException ("Multiple Context's are not supported by this device");

			if (Mode.Config == null)
				Mode.Initialize (window.Display, major);

			/*
			 * Create an OpenGL ES context. We want to do this as rarely as possible, an
			 * OpenGL context is a somewhat heavy object.
			 */
			int EglContextClientVersion = 0x3098;
			int EglContextMinorVersion = 0x30fb;
			int[] attribList = null;
			if (major >= 2) {
				string extensions = egl.EglQueryString (window.Display, Egl.Egl.EXTENSIONS);
				if (minor > 0 && !string.IsNullOrEmpty (extensions) && extensions.Contains ("EGL_KHR_create_context")) {
					attribList = new int [] { EglContextClientVersion, major,
						EglContextMinorVersion, minor,
						EGL10.EglNone
					};
				} else {
					attribList = new int [] { EglContextClientVersion, major,
						EGL10.EglNone
					};
				}
			}

			EGLContext = egl.EglCreateContext (window.Display,
						EGLConfig,
						shared != null && shared.EGLContext != null ? shared.EGLContext : EGL10.EglNoContext,
						attribList);

			if (EGLContext == EGL10.EglNoContext)
				throw EglException.GenerateException ("EglCreateContext == EGL10.EglNoContext", egl, null);

			if (shared != null && shared.EGLContext != null) {
				egl.EglMakeCurrent (window.Display, EGL10.EglNoSurface, EGL10.EglNoSurface, EGL10.EglNoContext);
				int[] pbufferAttribList = new int [] { EGL10.EglWidth, 64, EGL10.EglHeight, 64, EGL10.EglNone };
				surface = window.CreatePBufferSurface (EGLConfig, pbufferAttribList);
				if (surface == EGL10.EglNoSurface)
					throw new EglException ("Could not create PBuffer for shared context!");
			}
		}

		public bool Swap ()
		{
			bool ret = egl.EglSwapBuffers (window.Display, window.Surface);
			if (!ret) {
				int err = egl.EglGetError();
				switch (err) {
					case EGL11.EglContextLost:
						throw EglContextLostException.GenerateException ("EglSwapBuffers", egl, err);
					case EGL11.EglBadAlloc:
						throw EglBadAllocException.GenerateException ("EglSwapBuffers", egl, err);
					default:
						throw EglException.GenerateException ("EglSwapBuffers", egl, err);
				}
			}
			return ret;
		}

		public void SwapBuffers()
		{
			Swap ();
		}

		int IGraphicsContext.SwapInterval {
			get {throw new NotSupportedException();}
			set {throw new NotSupportedException();}
		}

		public void MakeCurrent (IWindowInfo win)
		{
			if (win == null) {
				ClearCurrent ();
				return;
			}
			var w = win as AndroidWindow;
			if (w == null)
				w = window;
			var surf = surface == null ? w.Surface : surface;
			var ctx = EGLContext;

			if (win == null) {
				surf = EGL10.EglNoSurface;
				ctx = EGL10.EglNoContext;
			}

			if (!egl.EglMakeCurrent (window.Display, surf, surf, ctx)) {
				int err = egl.EglGetError();
				switch (err) {
					case EGL11.EglContextLost:
						throw EglContextLostException.GenerateException ("MakeCurrent", egl, err);
					case EGL11.EglBadAlloc :
						throw EglBadAllocException.GenerateException ("MakeCurrent", egl, err);
					default:
						throw EglException.GenerateException ("MakeCurrent", egl, err);
				}
			}
		}

		void ClearCurrent() {
			if (window.Display != null)
				egl.EglMakeCurrent (window.Display, EGL10.EglNoSurface, EGL10.EglNoSurface, EGL10.EglNoContext);
		}

		void DestroyContext ()
		{
			if (EGLContext != null) {
				ClearCurrent ();
				egl.EglDestroyContext (window.Display, EGLContext);
				EGLContext = null;
			}
		}

		public void Update(IWindowInfo window)
		{
			MakeCurrent (null);
			MakeCurrent (window);
		}

		public void LoadAll()
		{
		}

		IntPtr IGraphicsContextInternal.GetAddress(string function)
		{
			return IntPtr.Zero;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing) {
					DestroyContext ();
					window = null;
					disposed = true;
				}
			}
		}

        public IntPtr GetAddress(IntPtr function) {
            return IntPtr.Zero;
        }

        ~AndroidGraphicsContext()
		{
			Dispose(false);
		}

		public bool IsCurrent {
			get {
				return egl.EglGetCurrentContext () == EGLContext;
			}
		}

		public bool IsDisposed {
			get { return disposed; }
		}

		public bool VSync {
			get { return false; }
			set { }
		}

		public GraphicsMode GraphicsMode {
			get; private set;
		}

		AndroidGraphicsMode Mode {
			get { return GraphicsMode as AndroidGraphicsMode; }
		}

		public bool ErrorChecking {
			get { return false; }
			set { }
		}

		IGraphicsContext IGraphicsContextInternal.Implementation {
			get { return this; }
		}

		ContextHandle IGraphicsContextInternal.Context {
			get { return new ContextHandle (EGLContext.Handle); }
		}
	}

	class EglException : InvalidOperationException
	{
		public static EglException GenerateException (string msg, IEGL10 egl, int? error)
		{
			if (egl == null)
				return new EglException (msg);
			error = error ?? egl.EglGetError ();
			if (error == EGL10.EglSuccess)
				return new EglException (msg);
			return new EglException (String.Format ("{0} failed with error {1} (0x{1:x})", msg, error.Value));
		}

		public EglException (string msg) : base (msg)
		{
		}
	}

	class EglContextLostException : EglException
	{
		public EglContextLostException (string msg) : base (msg)	{
		}
	}

	class EglBadAllocException : EglException
	{
		public EglBadAllocException (string msg) : base (msg) {
		}
	}

	public class AndroidWindow : IWindowInfo, IDisposable
	{
		bool disposed;
		WeakReference refHolder;

		public ISurfaceHolder Holder {
			get {
				return refHolder.Target as ISurfaceHolder;
			}
		}

		EGLDisplay eglDisplay;
		public EGLDisplay Display {
			get { return eglDisplay; }
			set {
				if (value == null && eglDisplay != null)
					TerminateDisplay ();
				eglDisplay = value;
			}
		}

		EGLSurface eglSurface;
		public EGLSurface Surface {
			get { return eglSurface; }
		}

        public IntPtr Handle { get { return eglDisplay != null ? eglDisplay.Handle : IntPtr.Zero; } }

        public AndroidWindow (ISurfaceHolder holder)
		{
			refHolder = new WeakReference (holder);
		}

		public void InitializeDisplay ()
		{
			if (eglDisplay != null && eglDisplay != EGL10.EglNoDisplay)
				return;

			IEGL10 egl = EGLContext.EGL.JavaCast<IEGL10> ();

			if (eglDisplay == null)
				eglDisplay = egl.EglGetDisplay (EGL10.EglDefaultDisplay);

			if (eglDisplay == EGL10.EglNoDisplay)
				throw EglException.GenerateException ("EglGetDisplay == EGL10.EglNoDisplay", egl, null);

			int[] version = new int[2];
			if (!egl.EglInitialize (eglDisplay, version)) {
				throw EglException.GenerateException ("EglInitialize", egl, null);
			}
		}

		public void CreateSurface (EGLConfig config)
		{
			if (refHolder == null) {
				CreatePBufferSurface (config);
				return;
			}

			IEGL10 egl = EGLContext.EGL.JavaCast<IEGL10> ();
			eglSurface = egl.EglCreateWindowSurface (eglDisplay, config, ((Java.Lang.Object)Holder), null);
			if (eglSurface == null || eglSurface == EGL10.EglNoSurface)
				throw EglException.GenerateException ("EglCreateWindowSurface", egl, null);
		}

		public EGLSurface CreatePBufferSurface (EGLConfig config, int[] attribList)
		{
			IEGL10 egl = EGLContext.EGL.JavaCast<IEGL10> ();
			EGLSurface result = egl.EglCreatePbufferSurface (eglDisplay, config, attribList);
			if (result == null || result == EGL10.EglNoSurface)
				throw EglException.GenerateException ("EglCreatePBufferSurface", egl, null);
			return result;
		}

		public void CreatePBufferSurface (EGLConfig config)
		{
			eglSurface = CreatePBufferSurface (config, null);
		}

/*
		public void CreatePixmapSurface (EGLConfig config)
		{
			Surface = egl.EglCreatePixmapSurface (Display, config, null ,null);
			if (Surface == null || Surface == EGL10.EglNoSurface)
				throw EglException.GenerateException ("EglCreatePixmapSurface", egl, null);
		}
*/
		public void DestroySurface ()
		{
			if (eglSurface != EGL10.EglNoSurface) {
				IEGL10 egl = EGLContext.EGL.JavaCast<IEGL10> ();
				try	{
					egl.EglMakeCurrent (eglDisplay, EGL10.EglNoSurface, EGL10.EglNoSurface, EGL10.EglNoContext);
					if (!egl.EglDestroySurface (eglDisplay, eglSurface))
						Log.Warn ("AndroidWindow", "Failed to destroy surface {0}.", eglSurface);
				}
				catch (Java.Lang.IllegalArgumentException)	{
					Log.Warn ("AndroidWindow", "Failed to destroy surface {0}. Illegal Argument", eglSurface);
				}
				eglSurface = null;
			}
		}

		public void TerminateDisplay ()
		{
			if (eglDisplay != null) {
				IEGL10 egl = EGLContext.EGL.JavaCast<IEGL10> ();
				if (!egl.EglTerminate (eglDisplay))
					Log.Warn ("AndroidWindow", "Failed to terminate display {0}.", eglDisplay);
				eglDisplay = null;
			}
		}

#region IDisposable Members

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		void Dispose (bool disposing)
		{
			if (!disposed)
			{
				if (disposing) {
					DestroySurface ();
					TerminateDisplay ();
					refHolder = null;
					disposed = true;
				}
			}
		}

		~AndroidWindow ()
		{
			Dispose (false);
		}

#endregion
	}
}
