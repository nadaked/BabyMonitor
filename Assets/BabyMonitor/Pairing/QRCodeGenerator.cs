using UnityEngine;
using ZXing;
using ZXing.QrCode;

namespace BabyMonitor.Pairing
{
    public static class QRCodeGenerator
    {
        public static Texture2D Generate(string content, int size = 256)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = size,
                    Height = size,
                    Margin = 1,
                    CharacterSet = "UTF-8"
                }
            };

            var pixels = writer.Write(content);

            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            // UI'da büyüdüğünde QR bulanıklaşmasın.
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            return texture;
        }
    }
}