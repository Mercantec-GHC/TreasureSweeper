  #pragma once
  #include <Arduino.h>
  #include <WiFiNINA.h>

  class ControllerInputs{
    public:
    enum { CAPACITY = 16, MAX_AGE_MS = 2500};
    uint8_t raw, stable, handled, first, count;
    uint8_t masks[CAPACITY];
    uint32_t changedAt, times[CAPACITY];
    bool overflow;

    void begin(){
      raw = stable = handled = first = count = 0;
      changedAt = 0;
      overflow = false;
    };
    void sample (uint8_t mask, uint32_t now){
      if (mask != raw){raw = mask; changedAt = now;}
      if (stable == raw || (uint32_t)(now - changedAt) < 35) return;
      stable = raw;
      if (count == CAPACITY) {first = count = 0; overflow = true;}
      uint8_t index = (first + count) % CAPACITY;
      masks[index] = stable;
      times[index] = now;
      ++count;
    };
    void clear(){ first = count = 0; handled = stable; overflow = false;}
    bool pending() const {return count != 0;}
    int next(uint32_t now, uint8_t & down, uint8_t & pressed, uint8_t &released){
      if (overflow || (count && (uint32_t )(now - times [first]) > MAX_AGE_MS)){
        clear(); down = stable; pressed = released = 0; return -1;
      }
      if (!count){ down = stable; pressed = released = 0; return 0;}
      down = masks [first];
      first = (first + 1) % CAPACITY;
      --count;
      pressed = down & ~handled;

      released = handled & ~down;
      handled = down; 
      return 1;
    };
  };
    class HttpBodyDecoder {
      public:
      enum State { LENGTH, CLOSED_BODY, SIZE, EXTENSION, SIZE_LF, DATA, DATA_CR, DATA_LF, TRAILER, TRAILER_LF, DONE, FAILED};
      State state;
      uint32_t remaining, chunksize, digits, lineLength, total;
      bool closeDelimited;
      void begin(bool chunked, long length){
        remaining = chunksize = digits = lineLength = total = 0;
        closeDelimited = !chunked && length < 0;
        if (chunked)state = SIZE;
        else if (length < 0) state = CLOSED_BODY;
        else { remaining = (uint32_t)length; state = remaining ? LENGTH : DONE; }
      }
      bool done() const { return state == DONE;}
      bool failed() const {return state == FAILED;}
      void fail() {state = FAILED;}
      void connectionClosed(){
        if (state == CLOSED_BODY) state= DONE;
        else if (state != DONE) state = FAILED;
      }
      int feed(uint8_t c){
        if (state == DONE || state == FAILED) return -2;
        if (state == LENGTH || state == DATA || state == CLOSED_BODY){
          if (++total > 262144UL){fail(); return -2;}
          if (state != CLOSED_BODY && --remaining == 0) state = state == LENGTH ? DONE : DATA_CR;
          return c;
        }
        switch (state){
          case SIZE: {
            int hex = c >= '0' && c <= '9' ? c - '0':
                      c >= 'a' && c <= 'f' ? c - 'a' + 10:
                      c >= 'A' && c <= 'F' ? c - 'A' + 10: -1;

              if (hex >= 0) {
                if (++digits > 8 || chunksize > (262144UL - (unsigned)hex) / 16) break;
                chunksize = chunksize * 16 + hex;
                return -1;
              }
              if (c == ';' && digits) { state = EXTENSION; lineLength = 0; return -1;}
              if (c == '\r'&& digits) { state = SIZE_LF; return -1;}
              break;
          }
          case EXTENSION:
          if (c == '\r') { state = SIZE_LF; return -1;}
          if (c >= 32 && ++lineLength <= 512) return -1;
          break;
          case SIZE_LF:
          if (c != '\n') break;
          remaining = chunksize;
          state = chunksize ? DATA : TRAILER;
          lineLength = 0;
          return -1;
          case DATA_CR:
          if (c == '\r') { state = DATA_LF; return -1;}
          break;
          case DATA_LF:
          if (c== '\n') { state = SIZE; chunksize = digits = 0; return -1;}
          break;
          case TRAILER:
          if (c == '\r') {state = TRAILER_LF; return -1;}
          if(c >= 32 && ++lineLength <= 1024) return -1;
          break;
          case TRAILER_LF:
          if (c !='\n') break;
          if (lineLength == 0) state = DONE;
          else { state = TRAILER; lineLength = 0;}
          return -1;
          default: break;
        }
        fail();
        return -2;
      }

    };
    class ControllerClient : public WiFiSSLClient{
      void (*pollCallback)() = nullptr;
      public:
      using WiFiSSLClient::write;
      void onPoll(void(*callback)()){pollCallback = callback;}
      void poll() {if (pollCallback)pollCallback();}
      int available() override {
        poll();
        return WiFiSSLClient::available();
      }

    };
    struct ControllerResponse {
      int status = 0;
      long length = -1;
      bool chunked = false;
      bool keepAlive = true;
    };
    inline bool controllerReadLine(ControllerClient &client, String &line, uint32_t started){
      line = "";
      uint32_t lastByte = millis();
      while((uint32_t)(millis() - lastByte) < 2500 && (uint32_t)(millis() - started) < 10000){
        client.poll();
        if (client.available()){
          int c = client.read();
          if (c < 0) continue;
          lastByte = millis();
          if (c == '\n') return true;
          if (c == '\r') continue;
          if (line.length() >= 512) return false;
          line += (char)c;
        }else {
          if (!client.connected()) return false;
          delay(1);
        }
      } 
      return false;
    };
    inline bool controllerReadHeaders(ControllerClient &client, ControllerResponse &response){
      uint32_t started = millis();
      String line;
      if (!controllerReadLine(client, line, started) ||
          !(line.startsWith("HTTP/1.1 ") || line.startsWith("HTTP/1.0"))) return false;
      response.status = line.substring(9, 12).toInt();
      if (response.status < 200 || response.status > 599) return false;
      response.keepAlive = line.startsWith("HTTP/1.1");
      bool connectionClose = false;
      size_t headerBytes = line.length();
      while (true){
        if (!controllerReadLine(client, line, started)) return false;
        headerBytes += line.length() + 2;
        if (headerBytes > 8192) return false;
        if (!line.length()) break;
        int colon = line.indexOf(':');
        if (colon <= 0) return false;
        String header = line.substring(0, colon);
        String value = line.substring(colon + 1);
        header.toLowerCase(); value.trim(); value.toLowerCase();
        if(header == "content-length"){
          if(!value.length()) return false;
          long length = 0;
          for (unsigned int i = 0; i < value.length(); ++i){
            if (value[i] < '0' || value[i] > '9') return false;
            length = length * 10 + value[i] - '0';
            if (length > 262144L) return false;
          }
          if (response.length >= 0 && response.length != length) return false;
          response.length = length;
        }else if (header == "transfer-encoding"){
          if (value != "chunked") return false;
          response.chunked = true;
        }else if (header == "connection"){
          if (value.indexOf("close") >= 0) connectionClose = true;
          if (value.indexOf("keep-alive") >= 0) response.keepAlive = true;
        }else if (header == "content-encoding" && value != "identity"){
          return false;
        }
      }
      if (connectionClose) response.keepAlive = false;
      return true;
    };
    class ControllerBodyStream : public Stream {
      ControllerClient &client;
      HttpBodyDecoder decoder;
      uint32_t started, lastByte;
      int lookahead = -1;
      public:
      ControllerBodyStream(ControllerClient &client, bool chunked, long length)
      : client(client), started(millis()), lastByte(started){
        decoder.begin(chunked, length);
        setTimeout(1);
      }
      bool done() const {return decoder.done();}
      bool failed() const {return decoder.failed();}
      bool reusable() const {return done() && !decoder.closeDelimited;}
      size_t write(uint8_t) override {return 0;}
      int available() override {
        if (lookahead >= 0) return 1;
        return done() || failed() ? 0 : client.available();
      }
      int peek() override {
        if (lookahead < 0) lookahead = read();
        return lookahead;
      }
      int read() override {
        if (lookahead >= 0) {int b = lookahead; lookahead = -1; return b;}
        while (!done() && !failed()) {
          client.poll();
          if ((uint32_t)(millis() - lastByte) >= 2500 ||
            (uint32_t)(millis() - started) >= 10000) {decoder.fail(); return -1;}
            if (!client.available()){
              if(!client.connected()) {decoder.connectionClosed(); return -1;}
              delay(1);
              continue;
            }
            int raw = client.read();
            if (raw < 0) continue;
            lastByte = millis();
            int decoded = decoder.feed((uint8_t)raw);
            if (decoded >= 0) return decoded;
        }
        return -1;
      }
      bool finish() {
        while (!done() && !failed()) read();
        return done();
      }
    };