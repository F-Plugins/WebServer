using dnlib.DotNet.Pdb;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace WebServer
{
    public static class Extensions
    {
        public static void SendResult(this HttpListenerResponse response, byte[] buffer)
        {
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);

            output.Close();
        }

        public static async Task<byte[]> ReadBytesFromStreamAsync(this Stream stream)
        {
            var buffer = new byte[stream.Length];

            await stream.ReadAsync(buffer, 0, (int)stream.Length);

            return buffer;
        }
    }
}
