/* Date: 27.9.2016, Time: 10:22 */
using System;
using System.IO;

namespace wavcomp
{
	internal class SkipStream : Stream
	{
		private readonly Stream baseStream;
		private int skipBytes;
		
		public SkipStream(Stream baseStream, int skipBytes)
		{
			this.baseStream = baseStream;
			this.skipBytes = skipBytes;
		}
		
		public override void Write(byte[] buffer, int offset, int count)
		{
			int newOffset = Math.Min(skipBytes, count);
			baseStream.Write(buffer, offset+newOffset, count-newOffset);
			skipBytes -= newOffset;
		}
		
		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}
		
		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}
		
		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}
		
		public override long Position{
			get{
				throw new NotImplementedException();
			}
			set{
				throw new NotImplementedException();
			}
		}
		
		public override long Length{
			get{
				throw new NotImplementedException();
			}
		}
		
		public override void Flush()
		{
			baseStream.Flush();
		}
		
		public override bool CanWrite{
			get{
				return baseStream.CanWrite;
			}
		}
		
		public override bool CanSeek{
			get{
				return false;
			}
		}
		
		public override bool CanRead{
			get{
				return false;
			}
		}
	}
}
