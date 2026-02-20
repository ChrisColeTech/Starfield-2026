using System.Text;

namespace MiniToolbox.Core.Bntx;

internal static class EncodingExtensions
{
	internal static string GetString(this Encoding encoding, byte[] bytes)
	{
		return encoding.GetString(bytes, 0, bytes.Length);
	}
}
