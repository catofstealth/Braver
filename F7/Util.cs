﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace F7 {
    public static class Util {
        public static int MakePowerOfTwo(int i) {
            int n = 1;
            while (n < i)
                n <<= 1;
            return n;
        }

        public static Texture2D LoadTex(this GraphicsDevice graphics, Ficedula.FF7.TexFile tex, int palette) {
            var texture = new Texture2D(graphics, tex.Width, tex.Height, false, SurfaceFormat.Color); //TODO MIPMAPS!
            texture.SetData(
                tex.ApplyPalette(palette)
                .SelectMany(row => row)
                .ToArray()
            );
            return texture;
        }

        public static Vector2 ToX(this System.Numerics.Vector2 v) {
            return new Vector2(v.X, v.Y);
        }
        public static Vector3 ToX(this System.Numerics.Vector3 v) {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Color WithAlpha(this Color c, byte alpha) {
            c.A = alpha;
            return c;
        }
    }

    public class F7Exception : Exception {
        public F7Exception(string msg) : base(msg) { }
    }

    public static class Serialisation {
        public static void Serialise(object o, System.IO.Stream s) {
            new System.Xml.Serialization.XmlSerializer(o.GetType()).Serialize(s, o);
        }

        public static T Deserialise<T>(System.IO.Stream s) {
            return (T)(new System.Xml.Serialization.XmlSerializer(typeof(T)).Deserialize(s));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionNormalColorTexture : IVertexType {
        public Vector3 Position;
        public Vector3 Normal;
        public Color Color;
        public Vector2 TexCoord;

        public static VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
              new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
              new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
              new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
              new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
        );
        VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
    }

}
