# RTL SDR Receiver

<i>.NET 8.0 DAB+/FM radio</i>

- DAB+ radio
  - OFDM Demodulator (Fast Fourier Transform)
  - Viterbi convolution decoding
  - Reed–Solomon forward error correction  
  - FIC channnel data parsing
  - AAC decoding (faad2)  

- FM radio
  - Mono FM demodulator 
  - Deemphasis filter  

- Platforms
	- Linux (console, UNO)
	- Windows (console, UNO)
	- Android (MAUI, UNO, not released yet) 

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

- Console using

    - DAB+
    
      - to see list of DAB+ servicies for 7C channel (192.352 MHz) using 2M sample rate: 
      ```
      RTLSDR.FMDAB.Console -dab -info -f 192352000 -sr 2048000
      ```

      - to play service number 3889:
      ```
      ./RTLSDR.FMDAB.Console -dab -f 192352000 -sr 2048000 -play -sn 3889
      ```
    
    - FM (mono only)

      - to play 96.9 MHz
      ```
      ./RTLSDR.FMDAB.Console.exe -fm -f 96900000 -play
      ```

- UNO GUI

    - UNO GUI is under construction and is very buggy
    - DAB+ only

<img src="https://raw.github.com/petrj/RTL-SDR-Receiver/master/Graphics/UNO.png" width="800" alt="UNO"/>

- there is only 1 optional command line argument: frequency in Hertz or frequency as constant like "8A" or "7C"

    ```
    RTLSDR.FMDAB.UNO.exe 192352000
    ```

    ```
    RTLSDR.FMDAB.UNO.exe 7C
    ```