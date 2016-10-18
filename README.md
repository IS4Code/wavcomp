# wavcomp
An efficient tool to pack simple WAV files, mainly those that contain beeps and FM synthesised sound (produced by pcsconv, my other tool). In these cases, it performs better than FLAC and can reduce the input to less than 1 % the original size.
The packed WAV files are a proprietary format designated for this software, and may not be playable in other places. The output is also usually compressed.
Please note that non-PCM WAV files may not work without problems at the moment.

## Usage
### Packing a file

    wavcomp unpacked.wav packed.wav #Packs the file with the default (bzip2) algorithm
    wavcomp -c best unpacked.wav packed.wav #Packs the file with the best possible algorithm (tries them all)
    
Please note that the FLAC support is limited in this tool, and it may not compress the file with the smallest compression ratio. In this case, try another FLAC compressor.

### Unpacking a file

    wavcomp packed.wav unpacked.wav
    
### Playing a file

    wavcomp input.wav
    
This plays all supported WAV files, packed or unpacked. 

### Display file info

    wavcomp -i input.wav
    
This only displays information about a file, including the format, compression and encryption method.