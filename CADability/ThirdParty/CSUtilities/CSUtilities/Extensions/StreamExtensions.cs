using System.IO;

namespace CSUtilities.Extensions
{
	/// <summary>
	/// Stream utility extensions.
	/// </summary>
#if PUBLIC
	public
#else
	internal
#endif
	static class StreamExtensions
	{
#if NETFRAMEWORK
		/// <summary>
		/// When overridden in a derived class, writes a sequence of bytes to the current
		/// stream and advances the current position within this stream by the number of
		/// bytes written.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="buffer">A region of memory. This method copies the contents of this region to the current stream. </param>
		public static void Write(this Stream stream, byte[] buffer)
		{
			stream.Write(buffer, 0, buffer.Length);
		}
#endif
	}
}
