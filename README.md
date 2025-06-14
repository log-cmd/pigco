# PC->(UDP)->pico->(USB)->Switch

## pigco
マイコン側プログラム C++  
UDPサーバーでパッドの入力を待ち受ける

プロコンとしてふるまう部分
https://github.com/DavidPagels/retro-pico-switch

### 動作確認
- Raspberry Pi Pico W
- Waveshare RP2350-ETH

## pigco-input
PC側プログラム C# WPF  
RawInputのキーボード・マウスをスプラトゥーン3に適した操作に変換