# RTL SDR Receiver

<i>.NET 9.0 DAB+/FM radio</i>

- DAB+ radio
  - OFDM Demodulator (Fast Fourier Transform)
  - Viterbi convolution decoding
  - Reed–Solomon forward error correction  
  - FIC channnel data parsing
  - AAC decoding (faad2)  

- FM radio
  - Mono/Stereo FM demodulator 
  - Deemphasis filter  

- UI
  - Text only (Console)
    - Linux
    - Windows  

- Dependencies
  - <a href="https://github.com/osmocom/rtl-sdr">rtl-sdr</a> 
  - <a href="https://github.com/knik0/faad2">faad2</a> for AAC decoding  


<img src="https://raw.github.com/petrj/RTL-SDR-Receiver/master/Graphics/DAB+Scheme.png" width="800" alt="Scheme"/>

- Instalation
  - Linux
    - sudo apt-get install libfaad2 rtl-sdr libasound2 libasound2-dev
    - extract release zip package    
  - Windows
    - install RTL2832U driver (Zadig)
    - download rtl-sdr windows binaries (<a href="https://ftp.osmocom.org/binaries/windows/rtl-sdr/">https://ftp.osmocom.org/binaries/windows/rtl-sdr/</a>) 
    - download (or build from source) libfaad2.dll
    - Modify PATH varible (or copy libfaad2.dll and rtl-sdr to suitable folder) to make the libraries visible
      ( I'm using this windows folder "c:\users\petrj\.dotnet\Tools" with theese files:
        libfaad2.dll
        libfaad2_dll.dll
        librtlsdr.dll
        libusb-1.0.dll
        libwinpthread-1.dll
        rtl_tcp.exe)
    - extract release zip package

- Console usage:

    - DAB+

      Tune 8D frequency and list all radio servicies:
      ```
      ./RTLSDR.FMDAB.Console -dab -f 8D
      ```


      Tune 8C frequency and play radio corresponding to service number 1175: 
      ```
      ./RTLSDR.FMDAB.Console -dab -f 8C -sn "1175"
      ```

      Tune 8C frequency, play radio corresponding to service number 1175 and save audio to PCM: 
      ```
      ./RTLSDR.FMDAB.Console -dab -f 8C -sn "1175" -ofile /tmp/radio.wave
      ```

    - FM

      Tune and play 104 MHz
      ```
      ./RTLSDR.FMDAB.Console.exe -fm -f "104 MHz"
      ```
