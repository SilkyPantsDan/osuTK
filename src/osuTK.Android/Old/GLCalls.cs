/* Licensed under the MIT/X11 license.
 * Copyright (c) 2011 Xamarin, Inc.
 * Copyright 2013 Xamarin Inc
 * This notice may not be removed from any source distribution.
 * See license.txt for licensing detailed licensing details.
 */

using System;
using osuTK;
using osuTK.Graphics;
using osuTK.Platform;
using osuTK.Platform.Android;

using All  = osuTK.Graphics.ES11.All;

using ES11 = osuTK.Graphics.ES11;
using ES20 = osuTK.Graphics.ES20;
using ES30 = osuTK.Graphics.ES30;

namespace osuTK
{
    internal sealed class GLCalls
    {
        public GLVersion Version;

        public delegate void glScissor (int x, int y, int width, int height);
        public delegate void glViewport (int x, int y, int width, int height);

        public glScissor Scissor;
        public glViewport Viewport;

        public static GLCalls GetGLCalls (GLVersion api)
        {
            switch (api) {
            case GLVersion.ES1:
                return CreateES1 ();
            case GLVersion.ES2:
                return CreateES2 ();
            case GLVersion.ES3:
                return CreateES3 ();
            }
            throw new ArgumentException ("api");
        }

        public static GLCalls CreateES1 ()
        {
            return new GLCalls () {
                Version                 = GLVersion.ES1,
                Scissor                 = (x, y, w, h)        => ES11.GL.Scissor(x, y, w, h),
                Viewport                = (x, y, w, h)        => ES11.GL.Viewport(x, y, w, h),
            };
        }

        public static GLCalls CreateES2 ()
        {
            return new GLCalls () {
                Version                 = GLVersion.ES2,
                Scissor                 = (x, y, w, h)        => ES20.GL.Scissor(x, y, w, h),
                Viewport                = (x, y, w, h)        => ES20.GL.Viewport(x, y, w, h),
                };
        }

        public static GLCalls CreateES3 ()
        {
            return new GLCalls () {
                Version                 = GLVersion.ES3,
                Scissor                 = (x, y, w, h)        => ES30.GL.Scissor(x, y, w, h),
                Viewport                = (x, y, w, h)        => ES30.GL.Viewport(x, y, w, h),
                };
        }
    }
}
