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
	- Linux: console
	- Windows: console   
	- Android/Windows: MAUI (under construction)

- Dependencies
  - <a href="https://github.com/knik0/faad2">faad2</a>
  - <a href="https://github.com/osmocom/rtl-sdr">rtl-sdr</a>

<img src="https://raw.github.com/petrj/RTL-SDR-Receiver/master/DAB+Scheme.png" width="800" alt="Scheme"/>