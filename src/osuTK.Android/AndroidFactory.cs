#region --- License ---
/* Licensed under the MIT/X11 license.
 * Copyright (c) 2011 Xamarin, Inc.
 * Copyright 2013 Xamarin Inc
 * This notice may not be removed from any source distribution.
 * See license.txt for licensing detailed licensing details.
 */
#endregion

using System;
using System.Diagnostics;
using osuTK.Graphics;
using Javax.Microedition.Khronos.Egl;
using osuTK.Input;

namespace osuTK.Platform.Android
{
    class AndroidFactory : IPlatformFactory
    {
        #region IPlatformFactory Members

        public virtual INativeWindow CreateNativeWindow(int x, int y, int width, int height, string title, GraphicsMode mode, GameWindowFlags options, DisplayDevice device)
        {
			throw new NotImplementedException ();
        }

        public virtual IDisplayDeviceDriver CreateDisplayDeviceDriver()
        {
			return new AndroidDisplayDeviceDriver ();
        }

        public virtual IGraphicsContext CreateGLContext(GraphicsMode mode, IWindowInfo window, IGraphicsContext shareContext, bool directRendering, int major, int minor, GraphicsContextFlags flags)
        {
			return new Android.AndroidGraphicsContext(mode, window, shareContext, major, minor, flags);
        }

        public virtual IGraphicsContext CreateGLContext(ContextHandle handle, IWindowInfo window, IGraphicsContext shareContext, bool directRendering, int major, int minor, GraphicsContextFlags flags)
        {
			throw new NotImplementedException ();
        }

        public virtual GraphicsContext.GetCurrentContextDelegate CreateGetCurrentGraphicsContext()
        {
            return (GraphicsContext.GetCurrentContextDelegate)delegate
            {
				try {
					var egl = global::Android.Runtime.Extensions.JavaCast<IEGL10> (EGLContext.EGL);
					var ctx = egl.EglGetCurrentContext ();
					if (ctx != null && ctx != EGL10.EglNoContext)
						return new ContextHandle (ctx.Handle);
				} catch (Exception ex) {
					global::Android.Util.Log.Error ("AndroidFactory", "Could not get the current EGLContext. {0}", ex);
				}
                return new ContextHandle(IntPtr.Zero);
            };
        }

        public virtual IGraphicsMode CreateGraphicsMode()
        {
			return new AndroidGraphicsMode ();
        }

        public virtual osuTK.Input.IKeyboardDriver2 CreateKeyboardDriver()
        {
            throw new NotImplementedException();
        }

        public virtual osuTK.Input.IMouseDriver2 CreateMouseDriver()
        {
            throw new NotImplementedException();
        }

        public IGamePadDriver CreateGamePadDriver() {
            throw new NotImplementedException();
        }

        public IJoystickDriver2 CreateJoystickDriver() {
            throw new NotImplementedException();
        }

        public void RegisterResource(IDisposable resource) {
            throw new NotImplementedException();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~AndroidFactory() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion
    }
}
