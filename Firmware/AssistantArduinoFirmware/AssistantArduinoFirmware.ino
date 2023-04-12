/***************************************************
  This is our GFX example for the Adafruit ILI9341 Breakout and Shield
  ----> http://www.adafruit.com/products/1651

  Check out the links above for our tutorials and wiring diagrams
  These displays use SPI to communicate, 4 or 5 pins are required to
  interface (RST is optional)
  Adafruit invests time and resources providing this open source code,
  please support Adafruit and open-source hardware by purchasing
  products from Adafruit!

  Written by Limor Fried/Ladyada for Adafruit Industries.
  MIT license, all text above must be included in any redistribution
 ****************************************************/
#define ILI9341_BLACK       0x0000  ///<   0,   0,   0
#define ILI9341_NAVY        0x000F  ///<   0,   0, 123
#define ILI9341_DARKGREEN   0x03E0  ///<   0, 125,   0
#define ILI9341_DARKCYAN    0x03EF  ///<   0, 125, 123
#define ILI9341_MAROON      0x7800  ///< 123,   0,   0
#define ILI9341_PURPLE      0x780F  ///< 123,   0, 123
#define ILI9341_OLIVE       0x7BE0  ///< 123, 125,   0
#define ILI9341_LIGHTGREY   0xC618  ///< 198, 195, 198
#define ILI9341_DARKGREY    0x7BEF  ///< 123, 125, 123
#define ILI9341_BLUE        0x001F  ///<   0,   0, 255
#define ILI9341_GREEN       0x07E0  ///<   0, 255,   0
#define ILI9341_CYAN        0x07FF  ///<   0, 255, 255
#define ILI9341_RED         0xF800  ///< 255,   0,   0
#define ILI9341_MAGENTA     0xF81F  ///< 255,   0, 255
#define ILI9341_YELLOW      0xFFE0  ///< 255, 255,   0
#define ILI9341_WHITE       0xFFFF  ///< 255, 255, 255
#define ILI9341_ORANGE      0xFD20  ///< 255, 165,   0
#define ILI9341_GREENYELLOW 0xAFE5  ///< 173, 255,  41
#define ILI9341_PINK        0xFC18  ///< 255, 130, 198

#include "SPI.h"
#include "Adafruit_GFX.h"
#include "Adafruit_ILI9341.h"

// For the Adafruit shield, these are the default.
#define TFT_CLK 13
#define TFT_MISO 11
#define TFT_MOSI 3
#define TFT_DC 5
#define TFT_CS 6
#define TFT_RST 9
// Use hardware SPI (on Uno, #13, #12, #11) and the above for CS/DC
//Adafruit_ILI9341 tft = Adafruit_ILI9341(TFT_CS, TFT_DC);
// If using the breakout, change pins as desired
Adafruit_ILI9341 tft = Adafruit_ILI9341(TFT_CS, TFT_DC, TFT_MOSI, TFT_CLK, TFT_RST, TFT_MISO);

String currentEmote = "^_^";
String currentText = "...";

void setup() {
  Serial.begin(9600);
  Serial.println("ILI9341 Test!"); 
 
  tft.begin();

  // read diagnostics (optional but can help debug problems)
  uint8_t x = tft.readcommand8(ILI9341_RDMODE);
  Serial.print("Display Power Mode: 0x"); Serial.println(x, HEX);
  x = tft.readcommand8(ILI9341_RDMADCTL);
  Serial.print("MADCTL Mode: 0x"); Serial.println(x, HEX);
  x = tft.readcommand8(ILI9341_RDPIXFMT);
  Serial.print("Pixel Format: 0x"); Serial.println(x, HEX);
  x = tft.readcommand8(ILI9341_RDIMGFMT);
  Serial.print("Image Format: 0x"); Serial.println(x, HEX);
  x = tft.readcommand8(ILI9341_RDSELFDIAG);
  Serial.print("Self Diagnostic: 0x"); Serial.println(x, HEX); 
  tft.setRotation(2);
  tft.fillScreen(ILI9341_NAVY);
  currentEmote = "^_^";
  currentText = "Epic AI Assistant At Your Service!";
  waitForUnity();
}

int aiState = -1;
int lastAiState = -2;
String lastUnityMessage = "Debug Message";
void loop(void) {
  if(Serial.available() > 0) {
    String input = Serial.readString();
    aiState = getValue(input,'\n',0).toInt();
    if(aiState == 4)lastUnityMessage = getValue(input,'\n',1);   
    Serial.print("Order recieved: " + String(aiState));
    if(aiState == 4) Serial.print(" (With the message: " + lastUnityMessage + ")");
    Serial.println();
    if(aiState != lastAiState) {
      switch(aiState) {
        case -1: waitForUnity(); break;
        case 0: waitForUser(); break;
        case 1: listenToUser(); break;
        case 2: think(); break;
        case 3: thinkHarder(); break;
        case 4: speak(lastUnityMessage); break;
      }
      lastAiState = aiState;
    }
    delay(500);
  }
  delay(50); 
}

String getValue(String data, char separator, int index)
{
    int found = 0;
    int strIndex[] = { 0, -1 };
    int maxIndex = data.length() - 1;

    for (int i = 0; i <= maxIndex && found <= index; i++) {
        if (data.charAt(i) == separator || i == maxIndex) {
            found++;
            strIndex[0] = strIndex[1] + 1;
            strIndex[1] = (i == maxIndex) ? i+1 : i;
        }
    }
    return found > index ? data.substring(strIndex[0], strIndex[1]) : "";
}

void waitForUnity() {
  currentEmote = "(-_-)zzz";
  currentText = "Waiting for unity to connect...";
  updateDisplay();
}

void speak(String text) {
  currentEmote = "(^o^)";
  currentText = text;
  updateDisplay();
}

void waitForUser() {
  currentEmote = "(0-0)";
  currentText = "Press the space bar to talk to me!";
  updateDisplay();
}

void listenToUser() {
  currentEmote = "(-_<)";
  currentText = "Listening to you!";
  updateDisplay();
}

void think() {
  currentEmote = "(>_<?)";
  currentText = "thinking....";
  updateDisplay();
}

void thinkHarder() {
  currentEmote = "(>_<')";
  currentText = "thinking harder....";
  updateDisplay();
}

String lastTextA = "";
String lastTextB = ""; 

void updateDisplay() {
  //Only overwriting the text is faster than clearing the entire screen
  tft.setCursor(0, 0);
  tft.setTextColor(ILI9341_NAVY);
  tft.setTextSize(4);
  tft.println(lastTextA);
  tft.setTextSize(2);
  tft.println(lastTextB);

  tft.setCursor(0, 0);
  tft.setTextColor(ILI9341_CYAN);
  tft.setTextSize(4);
  tft.println(currentEmote);
  tft.setTextColor(ILI9341_GREEN);
  tft.setTextSize(2);
  tft.println(currentText);
  lastTextA = currentEmote;
  lastTextB = currentText;
}
