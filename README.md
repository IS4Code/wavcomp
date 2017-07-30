# wavcomp
An efficient tool to pack simple WAV files, mainly those that contain beeps and FM synthesised sound (produced by [pcsconv](https://github.com/IllidanS4/pcsconv), my other tool). In these cases, it performs better than FLAC and can reduce the input to less than 1 % the original size.
The packed WAV files are a proprietary format designated for this software, and may not be playable in other places. The output is also usually encrypted.
Please note that non-PCM WAV files may not work without problems at the moment.

## Usage
### Packing a file

    wavcomp unpacked.wav packed.wav #Packs the file with the default (bzip2) algorithm
    wavcomp -c best unpacked.wav packed.wav #Packs the file with the best possible algorithm (tries them all)
    
Please note that the FLAC support is limited in this tool, and it may not compress the file with the smallest compression ratio. In this case, try another FLAC compressor.
Options are `-c method` for setting the compression method (none, gzip, lzma, flac, bzip2 - default, or best) and `-e method` for setting the encryption method (none, aes - default). When playing a WAV file, you can also specify the volume scale by `-v volume` (0 to 1).

### Unpacking a file

    wavcomp packed.wav unpacked.wav
    
### Playing a file

    wavcomp input.wav
    
This plays all supported WAV files, packed or unpacked. 

### Display file info

    wavcomp -i input.wav
    
This only displays information about a file, including the format, compression and encryption method.

## Technical details
The packed WAV files are identified by the `0xFFFE` format code (extensible), and the subformat is specified by its GUID in the extended WAVE header, followed by the compression and encryption method used, and the original format.
For the current version, the subformat is identified as `b99d5b0f4787b845810f0d56c02499f7` `{0f5b9db9-8747-45b8-810f-0d56c02499f7}`. To ensure the correct duration of the file is displayed, a `fact` chunk is created to specify the number of samples in the file.