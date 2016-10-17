/* Date: 28.9.2016, Time: 15:28 */
using System;
using System.IO;
using System.Linq;
using FlacBox;

namespace wavpack
{
	class FlacStream : Stream
	{
		readonly Stream baseStream;
		readonly bool keepOpen;
		readonly int bitsPerSample;
		readonly FlacReader reader;
		readonly FlacWriter writer;
		bool disposed;
		
		public FlacStream(Stream baseStream, bool keepOpen, int bitsPerSample)
		{
			this.baseStream = baseStream;
			this.keepOpen = keepOpen;
			this.bitsPerSample = bitsPerSample;
			
			reader = new FlacReader(baseStream, true);
			reader.RecordType = FlacBox.FlacRecordType.FrameFooter;
			writer = new FlacWriter(baseStream, true);
		}
		
		public override void Write(byte[] buffer, int offset, int count)
		{
			int[] samples = new int[count*8/bitsPerSample];
			if(bitsPerSample == 8)
			{
				for(int i = 0; i < count; i++)
				{
					samples[i] = buffer[offset+i];
				}
			}else unsafe{
				if(offset+count > buffer.Length) throw new IndexOutOfRangeException();
				fixed(byte* rawdata = buffer)
				{
					if(bitsPerSample == 16)
					{
						short* data = (short*)(rawdata+offset);
						for(int i = 0; i < count/2; i++)
						{
							samples[i] = data[i];
						}
					}else if(bitsPerSample == 32)
					{
						int* data = (int*)(rawdata+offset);
						for(int i = 0; i < count/4; i++)
						{
							samples[i] = data[i];
						}
					}
				}
			}
			
			
            var streaminfo = new FlacBox.FlacStreaminfo();
            streaminfo.ChannelsCount = 1;
            streaminfo.SampleRate = 44100;
            streaminfo.BitsPerSample = 16;
            streaminfo.MinBlockSize = 4608;
            streaminfo.MaxBlockSize = 4608;
            writer.StartStreamNoHeader(streaminfo);
			writer.WriteSamples(samples);
			writer.EndStream();
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
			int read = 0;
			while(reader.Read())
			{
				if(reader.RecordType == FlacBox.FlacRecordType.Subframe)
				{
					var samples = reader.GetValues().ToList();
					int num = samples.Count;
					
					if(bitsPerSample == 8)
					{
						for(int i = 0; i < num; i++)
						{
							buffer[i+offset+read] = (byte)samples[i];
						}
					}else unsafe{
						if(offset+count > buffer.Length) throw new IndexOutOfRangeException();
						fixed(byte* rawdata = buffer)
						{
							if(bitsPerSample == 16)
							{
								short* data = (short*)(rawdata+offset+read);
								for(int i = 0; i < num; i++)
								{
									data[i] = (short)samples[i];
								}
							}else if(bitsPerSample == 32)
							{
								int* data = (int*)(rawdata+offset+read);
								for(int i = 0; i < num; i++)
								{
									data[i] = samples[i];
								}
							}
						}
					}
					read += samples.Count*bitsPerSample/8;
				}
			}
			return read;
		}
		
		public override long Position{
			get{
				return baseStream.Position;
			}
			set{
				baseStream.Position = value;
			}
		}
		
		public override long Length{
			get{
				return baseStream.Length;
			}
		}
		
		public override void Flush()
		{
			throw new NotImplementedException();
		}
		
		public override bool CanWrite{
			get{
				throw new NotImplementedException();
			}
		}
		
		public override bool CanSeek{
			get{
				throw new NotImplementedException();
			}
		}
		
		public override bool CanRead{
			get{
				throw new NotImplementedException();
			}
		}
		
		protected override void Dispose(bool disposing)
		{
			if(!disposed)
			{
				disposed = true;
			}
			base.Dispose(disposing);
		}
	}
}
