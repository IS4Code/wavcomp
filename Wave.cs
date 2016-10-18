/* Date: 24.9.2016, Time: 13:30 */
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace wavcomp
{
	/// <summary>
	/// Description of Wave.
	/// </summary>
	public class Wave
	{
		/*private static readonly Dictionary<WaveFileOrigin, Guid> FormatGuids = new Dictionary<WaveFileOrigin, Guid>
		{
			{WaveFileOrigin.RawPCM, default(Guid)},
			{WaveFileOrigin.CompressedGZip, Guid.Parse("8b3b8f46-7b85-49e5-9da0-f24e69b7972d")},
			{WaveFileOrigin.CompressedDEFLATE, Guid.Parse("115626f9-15e1-4f43-80e8-85c8f007fcef")},
			{WaveFileOrigin.CompressedLZMA, Guid.Parse("d729c264-4afe-4335-aa4b-62107ccc4e4a")},
			{WaveFileOrigin.CompressedBzip2, Guid.Parse("0f5b9db9-8747-45b8-810f-0d56c02499f7")},
		};*/
		
		private static readonly Guid Version1Guid = Guid.Parse("0f5b9db9-8747-45b8-810f-0d56c02499f7");
		
		public short NumChannels{get; private set;}
		public ushort Format{get; private set;}
		public int SampleRate{get; private set;}
		public int ByteRate{get; private set;}
		public short BlockAlign{get; private set;}
		public short BitsPerSample{get; private set;}
		public byte[] RawData{get; private set;}
		public int NumSamples{get; private set;}
		
		public WaveCompression Compression{get; set;}
		public WaveEncryption Encryption{get; set;}
		
		public TimeSpan Duration{
			get{
				return TimeSpan.FromMilliseconds((double)NumSamples/SampleRate*1000);
			}
		}
		
		private readonly List<KeyValuePair<string,byte[]>> AdditionalChunks = new List<KeyValuePair<string,byte[]>>();
		
		public Wave(BinaryReader reader)
		{
			if(new string(reader.ReadChars(4)) != "RIFF") throw new CustomException("Not a valid WAVE file.");
			int filesize = reader.ReadInt32();
			if(new string(reader.ReadChars(4)) != "WAVE") throw new CustomException("Not a valid WAVE file.");
			
			while(true)
			{
				string type = new string(reader.ReadChars(4));
				if(type == "") break;
				switch(type)
				{
					case "fmt ": {
						int size = reader.ReadInt32();
						Format = reader.ReadUInt16();
						if(Format != 1 && Format != 0xFFFE) throw new CustomException("Only PCM WAVE files are supported.");
						if(size < 16) throw new CustomException("WAVE format not recognized.");
						NumChannels = reader.ReadInt16();
						SampleRate = reader.ReadInt32();
						ByteRate = reader.ReadInt32();
						BlockAlign = reader.ReadInt16();
						BitsPerSample = reader.ReadInt16();
						
						if(Format == 0xFFFE)
						{
							if(size < 18) throw new CustomException("Invalid WAVE header.");
							int cbSize = reader.ReadInt16();
							if(cbSize < 22) throw new CustomException("Extended WAVE format not recognized.");
							reader.ReadInt16();
							reader.ReadInt32();
							
							/*Guid fmtGuid = new Guid(reader.ReadBytes(16));
							
							Origin = FindFormatType(fmtGuid);
							if(Origin == 0) throw new CustomException("Compression method is not supported.");*/
							if(new Guid(reader.ReadBytes(16)) != Version1Guid) throw new CustomException("This is not a wavpack-compatible file.");
							if(cbSize != 26) throw new CustomException("The file appears to be created using a newer version of wavpack.");
							
							Compression = (WaveCompression)reader.ReadByte();
							Encryption = (WaveEncryption)reader.ReadByte();
							Format = reader.ReadUInt16();
							ByteRate = SampleRate * NumChannels * BitsPerSample/8;
						}else{
							reader.ReadBytes(size-16);
							Compression = WaveCompression.Uncompressed;
							Encryption = WaveEncryption.Unencrypted;
						}
						break;
					}
					case "fact": {
						int size = reader.ReadInt32();
						if(size < 4) throw new CustomException("Unrecognized 'fact' chunk format.");
						NumSamples = reader.ReadInt32();
						reader.ReadBytes(size-4);
						break;
					}
					case "data": {
						int size = reader.ReadInt32();
						RawData = reader.ReadBytes(size);
						if(Compression != WaveCompression.Uncompressed)
						{
							using(var buffer = new MemoryStream(RawData))
							{
								RawData = new byte[NumSamples * NumChannels * BitsPerSample/8];
								var dec = CreateEncryptionStream(buffer, Encryption, CryptoStreamMode.Read);
								using(var comp = CreateCompressionStream(dec, Compression, CompressionMode.Decompress))
								{
									int pos = 0, read;
									do{
										read = comp.Read(RawData, pos, RawData.Length-pos);
										pos += read;
									}while(read != 0);
								}
								//dec.Read(RawData, 0, RawData.Length);
							}
						}else{
							NumSamples = RawData.Length*8/BitsPerSample/NumChannels;
						}
						break;
					}
					default:
						AdditionalChunks.Add(new KeyValuePair<string,byte[]>(type, reader.ReadBytes(reader.ReadInt32())));
						break;
				}
			}
		}
		
		private Stream CreateCompressionStream(Stream stream, WaveCompression type, CompressionMode mode)
		{
			switch(type)
			{
				case WaveCompression.Uncompressed:
					return stream;
				case WaveCompression.GZip:
					return new GZipStream(stream, mode, true);
				case WaveCompression.DEFLATE:
					return new DeflateStream(stream, mode, true);
				case WaveCompression.LZMA:
					return mode == CompressionMode.Compress ? LzmaStream.Encode(stream) : LzmaStream.Decode(stream);
				case WaveCompression.Bzip2:
					return mode == CompressionMode.Compress ? Bzip2Stream.Output(new SkipStream(stream, 2), false, 9) : Bzip2Stream.Input(stream, true);
				case WaveCompression.Flac:
					return new FlacStream(stream, true, BitsPerSample);
				default:
					throw new CustomException("This compression method is not supported.");
			}
		}
		
		static class LzmaStream
		{
			public static Stream Encode(Stream outputStream)
			{
				return new SevenZip.LzmaEncodeStream(outputStream);
			}
			
			public static Stream Decode(Stream encodedStream)
			{
				return new SevenZip.LzmaDecodeStream(encodedStream);
			}
		}
		
		static class Bzip2Stream
		{
			public static Stream Output(Stream outputStream, bool isOwner, int blockSizeMultiplier)
			{
				return new Bzip2.BZip2OutputStream(outputStream, isOwner, blockSizeMultiplier);
			}
			
			public static Stream Input(Stream inputStream, bool headerless)
			{
				return new Bzip2.BZip2InputStream(inputStream, headerless);
			}
		}
		
		private static AesManaged CreateAes()
		{
			var AesEncryption = new AesManaged();
			AesEncryption.IV = new byte[]{218,49,51,226,179,245,189,106,104,210,199,149,155,119,212,164};
			AesEncryption.Key = new byte[]{119,231,71,225,191,43,37,69,15,32,146,85,253,5,32,227,25,3,217,23,104,128,153,37,23,28,7,97,20,100,149,1};
			AesEncryption.Padding = PaddingMode.PKCS7;
			return AesEncryption;
		}
		
		private static Stream CreateEncryptionStream(Stream stream, WaveEncryption type, CryptoStreamMode mode)
		{
			switch(type)
			{
				case WaveEncryption.Unencrypted:
					return stream;
				case WaveEncryption.AES:
					return new CryptoStream(stream, mode == CryptoStreamMode.Write ? CreateAes().CreateEncryptor() : CreateAes().CreateDecryptor(), mode);
				default:
					throw new CustomException("This encryption method is not supported.");
			}
		}
		
		public void Save(BinaryWriter writer)
		{
			int fmtSize = 16;
			int riffSize = 4+8+fmtSize+AdditionalChunks.Sum(c => 8+c.Value.Length)+8+RawData.Length;
			
			writer.Write("RIFF".ToCharArray());
			writer.Write(riffSize);
			writer.Write("WAVE".ToCharArray());
			
			writer.Write("fmt ".ToCharArray());
			writer.Write(fmtSize);
			writer.Write((short)1);
			writer.Write(NumChannels);
			writer.Write(SampleRate);
			writer.Write(ByteRate);
			writer.Write(BlockAlign);
			writer.Write(BitsPerSample);
			
			foreach(var chunk in AdditionalChunks)
			{
				writer.Write(chunk.Key.ToCharArray());
				writer.Write(chunk.Value.Length);
				writer.Write(chunk.Value);
			}
			
			writer.Write("data".ToCharArray());
			writer.Write(RawData.Length);
			writer.Write(RawData);
		}
		
		public void Pack(BinaryWriter writer, WaveCompression compression, WaveEncryption encryption)
		{
			byte[] cdata;
			using(var buffer = new MemoryStream())
			{
				var enc = CreateEncryptionStream(buffer, encryption, CryptoStreamMode.Write);
				if(compression == WaveCompression.LZMA) //needs buffer
				{
					using(var compbuffer = new MemoryStream())
					{
						using(var comp = CreateCompressionStream(compbuffer, compression, CompressionMode.Compress))
						{
							comp.Write(RawData, 0, RawData.Length);
						}
						compbuffer.Position = 0;
						compbuffer.CopyTo(enc);
					}
				}else using(var comp = CreateCompressionStream(enc, compression, CompressionMode.Compress))
				{
					comp.Write(RawData, 0, RawData.Length);
				}
				enc.Close();
				cdata = buffer.ToArray();
			}
			
			short cbSize = 2+4+16+4;
			int fmtSize = 16+2+cbSize;
			int riffSize = 4+8+fmtSize+8+4+AdditionalChunks.Sum(c => 8+c.Value.Length)+8+cdata.Length;
			
			writer.Write("RIFF".ToCharArray());
			writer.Write(riffSize);
			writer.Write("WAVE".ToCharArray());
			
			writer.Write("fmt ".ToCharArray());
			writer.Write(fmtSize);
			writer.Write((ushort)0xFFFE);
			writer.Write(NumChannels);
			writer.Write(SampleRate);
			writer.Write((int)Math.Round(cdata.Length/((float)NumSamples/SampleRate)));
			writer.Write(BlockAlign);
			writer.Write(BitsPerSample);
			
			writer.Write(cbSize);
			writer.Write((short)0);
			writer.Write(0);
			
			//writer.Write(FormatGuids[compressionMethod].ToByteArray());
			writer.Write(Version1Guid.ToByteArray());
			writer.Write((byte)compression);
			writer.Write((byte)encryption);
			writer.Write(Format);
			
			writer.Write("fact".ToCharArray());
			writer.Write(4);
			writer.Write(NumSamples);
			
			foreach(var chunk in AdditionalChunks)
			{
				writer.Write(chunk.Key.ToCharArray());
				writer.Write(chunk.Value.Length);
				writer.Write(chunk.Value);
			}
			
			writer.Write("data".ToCharArray());
			writer.Write(cdata.Length);
			writer.Write(cdata);
		}
		
		public void Pack(string file, WaveCompression compression, WaveEncryption encryption)
		{
			using(var stream = new FileStream(file, FileMode.Create))
			{
				Pack(stream, compression, encryption);
			}
		}
		
		public void Pack(Stream output, WaveCompression compression, WaveEncryption encryption)
		{
			Pack(new BinaryWriter(output), compression, encryption);
		}
		
		
		public void Save(string file)
		{
			using(var stream = new FileStream(file, FileMode.Create))
			{
				Save(stream);
			}
		}
		
		public void Save(Stream output)
		{
			Save(new BinaryWriter(output));
		}
		
		public static Wave Open(string file)
		{
			using(var stream = new FileStream(file, FileMode.Open, FileAccess.Read))
			{
				return Open(stream);
			}
		}
		
		public static Wave Open(Stream input)
		{
			var reader = new BinaryReader(input);
			return new Wave(reader);
		}
		
		public static IEnumerable<WaveCompression> CompressionMethods{
			get{
				var num = Enum.GetValues(typeof(WaveCompression)).Length;
				return Enumerable.Range(0, num).Select(i => (WaveCompression)i).Skip(2);
			}
		}
	}
	
	public enum WaveCompression : byte
	{
		Unknown = 0,
		Uncompressed = 1,
		GZip = 2,
		DEFLATE = 3,
		LZMA = 4,
		Bzip2 = 5,
		Flac = 6,
	}
	
	public enum WaveEncryption : byte
	{
		Unknown = 0,
		Unencrypted = 1,
		AES = 2,
	}
}
