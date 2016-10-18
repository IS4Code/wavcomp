/* Date: 24.9.2016, Time: 13:24 */
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;

namespace wavcomp
{
	class Program
	{
		static Program()
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
		}
		
		static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(args.Name);
			if(stream == null) return null;
			using(var buffer = new MemoryStream())
			{
				using(var gzip = new GZipStream(stream, CompressionMode.Decompress, true))
				{
					gzip.CopyTo(buffer);
				}
				return Assembly.Load(buffer.ToArray());
			}
		}
		
		public static void Main(string[] args)
		{
			string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString(2);
			Console.WriteLine("wavcomp v"+ver+" (2016) by IllidanS4");
			if(args.Length == 0)
			{
				ShowHelp();
				return;
			}
			
			var argiter = args.GetEnumerator();
			
			string input = null;
			string output = null;
			string compression = "";
			string encryption = "";
			bool justinfo = false;
			double volume = 1;
			
			while(argiter.MoveNext())
			{
				string arg = (string)argiter.Current;
				if(arg.StartsWith("-"))
				{
					switch(arg)
					{
						case "-c":
							if(!argiter.MoveNext())
							{
								ShowHelp();
								return;
							}
							compression = (string)argiter.Current;
							break;
						case "-e":
							if(!argiter.MoveNext())
							{
								ShowHelp();
								return;
							}
							encryption = (string)argiter.Current;
							break;
						case "-i":
							justinfo = true;
							break;
						case "-v":
							if(!argiter.MoveNext())
							{
								ShowHelp();
								return;
							}
							volume = Double.Parse((string)argiter.Current, CultureInfo.InvariantCulture);
							if(volume < 0 || volume > 1)
							{
								Console.WriteLine("Volume must be in range from 0 to 1.");
								return;
							}
							break;
						default:
							ShowHelp();
							return;
					}
				}else{
					if(input == null)
					{
						input = arg;
					}else if(output == null)
					{
						output = arg;
					}else{
						ShowHelp();
						return;
					}
				}
			}
			
			if(!File.Exists(input))
			{
				Console.WriteLine("Input file not found!");
				return;
			}
			
			try{
				Wave wave;
				long waveLength;
				using(var stream = new FileStream(input, FileMode.Open, FileAccess.Read))
				{
					wave = Wave.Open(stream);
					waveLength = stream.Length;
				}
				Console.WriteLine("Input file info:");
				Console.WriteLine("Format {0}, {1}-bit, {2} Hz, {3}", wave.Format==1?"PCM":wave.Format.ToString(), wave.BitsPerSample, wave.SampleRate, wave.Duration.ToString().TrimEnd('0'));
				if(wave.Compression != WaveCompression.Uncompressed || wave.Encryption != WaveEncryption.Unencrypted)
				{
					Console.WriteLine("Compression: {0}, encryption: {1}", wave.Compression, wave.Encryption);
				}
				if(output == null)
				{
					if(!justinfo)
					{
						Console.WriteLine("Playing file...");
						if(volume != 1)
						{
							ChangeWaveVolume(wave, volume);
						}
						using(var buffer = new MemoryStream())
						{
							wave.Save(buffer);
							buffer.Position = 0;
							var player = new SoundPlayer(buffer);
							player.PlaySync();
						}
					}
				}else if(wave.Compression == WaveCompression.Uncompressed || compression != "" || encryption != "")
				{
					Console.WriteLine("Packing file...");
					
					switch(encryption.ToLower(CultureInfo.InvariantCulture))
					{
						case "none":
							wave.Encryption = WaveEncryption.Unencrypted;
							break;
						case "aes":
						case "":
							wave.Encryption = WaveEncryption.AES;
							break;
						default:
							Console.WriteLine("Unknown encryption method.");
							return;
					}
					
					switch(compression.ToLower(CultureInfo.InvariantCulture))
					{
						case "none":
							wave.Compression = WaveCompression.Uncompressed;
							break;
						case "gzip":
							wave.Compression = WaveCompression.GZip;
							break;
						/*case "deflate":
							wave.Compression = WaveCompression.DEFLATE;
							break;*/
						case "lzma":
							wave.Compression = WaveCompression.LZMA;
							break;
						case "flac":
							wave.Compression = WaveCompression.Flac;
							break;
						case "bzip2":
						case "":
							wave.Compression = WaveCompression.Bzip2;
							break;
						case "best":
							Console.WriteLine("Determining the best compression...");
							
							var buffers = new MemoryStream[Enum.GetValues(typeof(WaveCompression)).Length];
							
							Parallel.ForEach(
								Wave.CompressionMethods,
								comp => {
									if(comp == WaveCompression.DEFLATE) return;
									var buffer = new MemoryStream();
									wave.Pack(buffer, comp, wave.Encryption);
									buffers[(int)comp] = buffer;
								}
							);
							
							MemoryStream smallestBuffer = null;
							int smallestIndex = 0;
							long smallestLength = Int64.MaxValue;
							for(int i = 0; i < buffers.Length; i++)
							{
								if(buffers[i] == null) continue;
								
								var len = buffers[i].Length;
								if(len > 0 && len < smallestLength)
								{
									smallestBuffer = buffers[i];
									smallestIndex = i;
									smallestLength = len;
								}
							}
							
							if(smallestLength < waveLength)
							{
								Console.WriteLine("The best compression is: {0}.", (WaveCompression)smallestIndex);
								using(var stream = new FileStream(output, FileMode.Create))
								{
									smallestBuffer.Position = 0;
									smallestBuffer.CopyTo(stream);
								}
								Console.WriteLine("Done!");
							}else{
								Console.WriteLine("No compression is appliable.");
							}
							return;
						default:
							Console.WriteLine("Unknown compression method.");
							return;
					}
					
					Console.WriteLine("{0}, encryption: {1}.", wave.Compression, wave.Encryption);
					wave.Pack(output, wave.Compression, wave.Encryption);
				}else{
					Console.WriteLine("Unpacking file...");
					wave.Save(output);
				}
				Console.WriteLine("Done!");
			}catch(CustomException e)
			{
				Console.WriteLine(e.Message);
			}catch(Exception e)
			{
				Console.WriteLine(e);
			}
		}
		
		private static void ShowHelp()
		{
			Console.WriteLine("Arguments:");
			Console.WriteLine("wavcomp [options] input [output]");
			Console.WriteLine("Options:");
			Console.WriteLine(" -c method ... Sets compression method (none,gzip,lzma,bzip2,flac,best).");
			Console.WriteLine(" -e method ... Sets encryption method (none,aes).");
			Console.WriteLine(" -i .......... Displays only file info.");
		}
		
		private static void ChangeWaveVolume(Wave wave, double volume)
		{
			if(wave.BitsPerSample == 8)
			{
				var data = wave.RawData;
				for(int i = 0; i < data.Length; i++)
				{
					data[i] = (byte)Math.Round(data[i]*volume);
				}
			}else unsafe{
				fixed(byte* rawdata = wave.RawData)
				{
					if(wave.BitsPerSample == 16)
					{
						short* data = (short*)rawdata;
						for(int i = 0; i < wave.RawData.Length/2; i++)
						{
							data[i] = (short)Math.Round(data[i]*volume);
						}
					}else if(wave.BitsPerSample == 32)
					{
						int* data = (int*)rawdata;
						for(int i = 0; i < wave.RawData.Length/4; i++)
						{
							data[i] = (int)Math.Round(data[i]*volume);
						}
					}
				}
			}
		}
	}
}