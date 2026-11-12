#include <Arduino_MKRIoTCarrier.h>
#include "ControllerTransport.h"
#include <WiFiNINA.h>
#include "config.h"
#include <ArduinoJson.h>
#include <WiFiSSLClient.h>

MKRIoTCarrier carrier;
uint32_t lastRepeat = 0;
int repeatedKey = -1;
const uint32_t REPEAT_MS = 170;
const uint8_t k0 = 1 << 0, k1 = 1 << 1, k2 = 1 << 2;
const uint8_t k3 = 1 << 3, k4 = 1 << 4;
ControllerInputs inputs;
uint8_t rawMask = 0;
uint8_t keys = 0;
uint32_t lastInputPoll = 0;

const uint32_t LIGHT_MS = 1500;
const uint32_t FREEZE_DURATION = 5000;
const uint32_t PING_STEP_MS = 100;
bool lightActive = false;
uint32_t lightStart = 0;
uint32_t lightDuration = 0;
bool pinging = false;
int pingFrame = -1;
uint8_t pingR = 0, pingG = 0;

ControllerClient transport;
String playerId;
int playerNumber = 0;
String playerColorHex = "";
String deviceId;
String lastError;
bool ready = false;
bool networkWasUp = false;
bool releaseBeforePlay = true;
bool networkUsed = false;
uint32_t lastJoinAttempt = 0;
uint32_t lastHeartbeat = 0;
int lastHttpStatus = 0;

const int16_t DISPLAY_CENTER = 120, RING_WIDTH = 10;            // 240x240 round display
const int16_t CELL = 30, GRID_X = DISPLAY_CENTER - CELL * 5 / 2, GRID_Y = GRID_X;
const uint8_t TILE_OUTSIDE = 255;
bool inRound = false;
bool gameScreenShown = false;
uint8_t view[25];
uint8_t shownView[25];
uint32_t viewHash = 0;
char facing = '?', shownFacing = 0;
int treasureDistance = -1, shownDistance = -2;

void readKeys(){
  if (millis() - lastInputPoll < 4) return;
  lastInputPoll = millis();
  if (carrier.Buttons.update()){
    rawMask = 0;
    if (carrier.Buttons.getTouch(TOUCH0)) rawMask |= k0;
    if (carrier.Buttons.getTouch(TOUCH1)) rawMask |= k1;
    if (carrier.Buttons.getTouch(TOUCH2)) rawMask |= k2;
    if (carrier.Buttons.getTouch(TOUCH3)) rawMask |= k3;
    if (carrier.Buttons.getTouch(TOUCH4)) rawMask |= k4;
  }
  inputs.sample(rawMask, millis());
  keys = inputs.stable;
}

uint32_t lastWifiAttempt = 0;
bool ensureWifi(){
  if(WiFi.status() == WL_CONNECTED){
    if(!networkWasUp){
      networkWasUp = true;
      ready = false;
      lastHeartbeat = millis() - HEARTBEAT_MS;
      if (!deviceId.length()){
        byte mac[6];
        WiFi.macAddress(mac);
        char id[18];
        snprintf(id, sizeof(id), "OPLA_%02x%02x%02x%02x%02x%02x",mac[0], mac[1], mac[2], mac[3], mac[4],mac[5]);
        deviceId = id;
      }
      Serial.println("WiFi tilslutter");
    }
    return true;
  }
  if (networkWasUp){
    networkWasUp = false;
    ready = false;
    releaseBeforePlay = true;
    transport.stop();
  }
  if (millis() - lastWifiAttempt >= 10000){
    lastWifiAttempt = millis();
    Serial.println("Forbinder til WiFi...");
    if ( WIFI_PASS[0]) WiFi.begin(WIFI_SSID, WIFI_PASS);
    else WiFi.begin(WIFI_SSID);
  }
  return false;
}

void resetControls(){
  inputs.clear();
  repeatedKey = -1;
}

void identity(JsonDocument &request) {
  request["deviceId"] = deviceId;
  if (playerId.length()) request["playerId"] = playerId;
  if (GAME_PIN[0]) request["pin"] = GAME_PIN;
  Serial.println(playerId);
}

void forgetSession(){
  playerId = "";
  ready = false;
  releaseBeforePlay = true;
  lastJoinAttempt = millis();
  resetControls();
}

bool requestJson(const char *endpoint, JsonDocument *request, JsonDocument &reply) {
  networkUsed = true;
  reply.clear();
  lastError = "";
  lastHttpStatus = 0;
  bool reused = transport.connected();
  ControllerResponse response;
  bool complete = false;
  if (!reused && !transport.connect(SERVER_HOST, SERVER_PORT)){
    lastError = "HTTPS: tjek net/certifikat";
  }else{
    String payload;
    if (request) serializeJson(*request, payload);
    String packet = request ? "POST " : "GET ";
    Serial.println(packet); //-------------------------------------------------------------
    Serial.println(endpoint); //---------------------------------------------------------------------
    Serial.println(payload); // ---------------------------------------------------------------------------
  packet += String(GAME_PATH) + endpoint + " HTTP/1.1\r\nHost: " + SERVER_HOST;
  packet += "\r\nAccept: application/json\r\nAccept-Encoding: identity";
  packet += "\r\nConnection: keep-alive\r\n";
  if(request){
    packet += "Content-Type: application/json\r\nContent-Length:";
    packet += String(payload.length()) + "\r\n";
  }
  packet += "\r\n";
  packet += payload;
  readKeys();
  Serial.println(packet.c_str()); //-----------------------------------------------------------------------------------------
  size_t sent = transport.write((const uint8_t *)packet.c_str(), packet.length());
  readKeys();
  if (sent != packet.length()){
    lastError ="kunne ikke sende hele beskeden";
  }else if (!controllerReadHeaders(transport, response)){
    lastError = "Serveren svarede ikke korrekt";
  }else{
    lastHttpStatus = response.status;
    ControllerBodyStream bodyStream(transport, response.chunked, response.length);
    JsonDocument filter;
    const char *fields[] = {"ok", "playerId", "error", "message", "event", "count", "number", "color",
                            "facing", "treasureDistance", "view", "viewHash"};
    for (const char *field : fields) filter[field] = true;
    DeserializationError error = deserializeJson(
      reply, bodyStream, DeserializationOption::Filter(filter));
      complete = !error && bodyStream.finish();
      if (!complete) lastError = "Ufuldstaendigt serversvar";
      if (!complete || !response.keepAlive || !bodyStream.reusable())
      transport.stop();
  }
  }
  if (!complete){
    transport.stop();
    ready = false;
    releaseBeforePlay = true;
    resetControls();
    lastHeartbeat = millis();
    Serial.println(lastError);
    return false;
  }
  String replyPayload; //---------------------------------------------------------
  serializeJson(reply, replyPayload); //---------------------------------------------------------
  Serial.println(replyPayload); //--------------------------------------------
  if (lastHttpStatus >= 200 && lastHttpStatus < 300) return true;
  lastError = reply["error"] | (reply["message"] | "Forespoergsel afvist");
  Serial.println(String("HTTP ") + lastHttpStatus + ": " + lastError);
  if(lastHttpStatus >= 500){
    ready = false;
    releaseBeforePlay = true;
    resetControls();
    lastHeartbeat = millis();
  }
  return false;
}

bool postController(const char *endpoint, JsonDocument &request, JsonDocument & reply){
  bool responseOk = requestJson(endpoint, &request, reply);
  if (!responseOk || !reply["ok"].as<bool>()){
    if(lastHttpStatus == 403 || lastHttpStatus == 404) forgetSession();
    if (responseOk){
      lastError = reply["error"] | (reply["message"] | "handling kan ikke bruges nu");
      Serial.println(lastError);
    }
    return false;
  }
  const char *event = reply["event"];
  if (event) onEvent(event);
  return true;
}

uint16_t parseColor(const String &hex){
  if (hex.length() < 7 || hex[0] != '#') return 0xFFFF;         // white fallback
  long v = strtol(hex.c_str() + 1, nullptr, 16);
  uint8_t r = (v >> 16) & 0xFF, g = (v >> 8) & 0xFF, b = v & 0xFF;
  // The display washes out mixed colours, so stretch to full saturation (grey stays as is).
  uint8_t lo = min(r, min(g, b)), hi = max(r, max(g, b));
  if (hi > lo) {
    r = (r - lo) * 255 / (hi - lo);
    g = (g - lo) * 255 / (hi - lo);
    b = (b - lo) * 255 / (hi - lo);
  }
  return carrier.display.color565(r, g, b);                     // hex -> RGB565
}

void showIdentity(){
  gameScreenShown = false;
  carrier.display.setRotation(180);
  carrier.display.fillScreen(0x0000);
  carrier.display.fillCircle(DISPLAY_CENTER, DISPLAY_CENTER, DISPLAY_CENTER, parseColor(playerColorHex));
  carrier.display.fillCircle(DISPLAY_CENTER, DISPLAY_CENTER, DISPLAY_CENTER - RING_WIDTH, 0x0000);
  carrier.display.setTextColor(0xFFFF);                         // white
  carrier.display.setTextSize(2);
  carrier.display.setCursor(80, 70);
  carrier.display.print("Player");
  carrier.display.setTextSize(8);
  carrier.display.setCursor(100, 100);
  carrier.display.print(playerNumber);
}

// Tile values are TileContent from the server (src/Domain/Boards/TileContent.cs).
uint16_t tileColor(uint8_t t) {
  switch (t) {
    case 144: return 0x8200;                                    // hole, brown
    case 160: return 0xFFE0;                                    // treasure, yellow
    case 192: return 0x07FF;                                    // freeze mine, cyan
    case 224: return 0xF800;                                    // point mine, red
  }
  return t >= 128 && t <= 136 ? 0xC618 : 0x4208;                // scanned light grey : unexplored dark grey
}

void drawTile(int i) {
  int16_t x = GRID_X + (i % 5) * CELL, y = GRID_Y + (i / 5) * CELL;
  uint8_t t = view[i];
  carrier.display.fillRect(x, y, CELL, CELL, 0x0000);
  if (t == TILE_OUTSIDE) return;
  carrier.display.fillRect(x + 1, y + 1, CELL - 2, CELL - 2, tileColor(t));
  if (t >= 128 && t <= 136) {
    carrier.display.setTextColor(0x0000);
    carrier.display.setTextSize(2);
    carrier.display.setCursor(x + 10, y + 8);
    carrier.display.print(t - 128);
  }
  if (i == 12) {                                                // the player, pointing up = facing
    int16_t cx = x + CELL / 2;
    carrier.display.fillTriangle(cx, y + 5, x + 6, y + CELL - 6, x + CELL - 6, y + CELL - 6,
                                 parseColor(playerColorHex));
  }
}

// Only redraws what changed, so the screen doesn't flicker on every heartbeat.
void renderGame(bool viewChanged) {
  bool full = !gameScreenShown;
  if (full) {
    carrier.display.fillCircle(DISPLAY_CENTER, DISPLAY_CENTER, DISPLAY_CENTER - RING_WIDTH, 0x0000);
    gameScreenShown = true;
  }
  if (full || viewChanged) {
    for (int i = 0; i < 25; i++) {
      if (full || view[i] != shownView[i]) drawTile(i);
      shownView[i] = view[i];
    }
  }
  if (full || facing != shownFacing) {
    carrier.display.fillRect(110, 20, 20, 18, 0x0000);
    carrier.display.setTextColor(0xFFFF);
    carrier.display.setTextSize(2);
    carrier.display.setCursor(115, 22);
    carrier.display.print(facing);
    shownFacing = facing;
  }
  if (full || treasureDistance != shownDistance) {
    carrier.display.fillRect(20, 150, 25, 16, 0x0000);
    if (treasureDistance >= 0) {
      carrier.display.setTextColor(0xFFE0);
      carrier.display.setTextSize(2);
      carrier.display.setCursor(treasureDistance < 10 ? 28 : 22, 150);
      carrier.display.print(treasureDistance);
    }
    shownDistance = treasureDistance;
  }
}

// The server only sends `view` while a round is running.
void applyGameState(JsonDocument &reply) {
  if (lastHttpStatus != 200) return;
  JsonArray tiles = reply["view"].as<JsonArray>();
  if (tiles.isNull() || tiles.size() != 25) {
    if (inRound) {
      inRound = false;
      treasureDistance = -1;
      showIdentity();
    }
    return;
  }
  uint32_t hash = reply["viewHash"] | 0UL;
  bool viewChanged = !gameScreenShown || hash == 0 || hash != viewHash;
  if (viewChanged) {
    for (int i = 0; i < 25; i++) view[i] = tiles[i].isNull() ? TILE_OUTSIDE : tiles[i].as<uint8_t>();
    viewHash = hash;
  }
  const char *f = reply["facing"];
  if (f && f[0]) facing = f[0];
  if (reply["treasureDistance"].is<int>()) treasureDistance = reply["treasureDistance"];
  inRound = true;
  renderGame(viewChanged);
}

void setLed(int i, uint8_t r, uint8_t g, uint8_t b) {
  carrier.leds.setPixelColor(i, r, g, b);
}

void setAllLeds(uint8_t r, uint8_t g, uint8_t b) {
  for (int i = 0; i < 5; i++) setLed(i, r, g, b);
  carrier.leds.show();
}

void startPing(uint8_t r, uint8_t g) {
  pingR = r;
  pingG = g;
  pinging = true;
  pingFrame = -1;
}

// Radar sweep: a beam spins round the 5 LEDs with a fading tail.
void drawPing(uint32_t elapsed) {
  int frame = elapsed / PING_STEP_MS;
  if (frame == pingFrame) return;
  pingFrame = frame;
  int head = frame % 5;
  for (int i = 0; i < 5; i++) {
    int age = (head - i + 5) % 5;
    uint8_t level = age == 0 ? 255 : age == 1 ? 70 : age == 2 ? 15 : 0;
    setLed(i, pingR * level / 255, pingG * level / 255, 0);
  }
  carrier.leds.show();
}

// Events from docs/controller-api.md; unknown events turn the LEDs off.
void onEvent(const char *event) {
  lightDuration = LIGHT_MS;
  pinging = false;
  if (strcmp(event, "mineAhead") == 0) startPing(255, 0);
  else if (strcmp(event, "clearAhead") == 0) startPing(0, 255);
  else if (strcmp(event, "treasure") == 0) setAllLeds(255, 180, 0);
  else if (strcmp(event, "pointMine") == 0) setAllLeds(255, 0, 0);
  else if (strcmp(event, "freezeMine") == 0) { setAllLeds(0, 255, 255); lightDuration = FREEZE_DURATION; }
  else { setAllLeds(0, 0, 0); lightActive = false; return; }
  lightActive = true;
  lightStart = millis();
}

void updateLight() {
  if (!lightActive) return;
  uint32_t elapsed = millis() - lightStart;
  if (elapsed >= lightDuration) {
    setAllLeds(0, 0, 0);
    lightActive = false;
    pinging = false;
  } else if (pinging) {
    drawPing(elapsed);
  }
}

// Keeps buttons and the scan animation alive while waiting on the network.
void pollWhileWaiting() {
  readKeys();
  updateLight();
}
void joinGame(){
  lastJoinAttempt = millis();
  JsonDocument request, reply;
  identity(request);
  request["name"] = PLAYER_NAME;
  if (!postController("/api/controller/join", request, reply)) return;
  String assignedId = reply["playerId"] | "";
  if (!assignedId.length()){
    Serial.println("serverngav intet playerId");
    return;
  }
  playerId = assignedId;
  playerNumber   = reply["number"] | 0;
  playerColorHex = reply["color"] | "#FFFFFF";
  inRound = false;
  showIdentity();
  Serial.print("Number "); Serial.println(playerNumber);
  Serial.print("Color "); Serial.println(playerColorHex);
  ready = true;
  releaseBeforePlay = true;
  resetControls();
  lastHeartbeat = millis();
  Serial.print("PlayerId ");
  Serial.println(playerId);
}

void heartbeat(){
  JsonDocument request, reply;
  identity(request);
  bool wasReady = ready;
  bool ok = postController("/api/controller/heartbeat", request, reply);
  lastHeartbeat = millis();
  applyGameState(reply);
  if (!ok){
    if (lastHttpStatus == 200 && reply["ok"].is<bool>() && !reply["ok"].as<bool>()) forgetSession();
    return;
  }
  ready = true;
  if (!wasReady){
    releaseBeforePlay = true;
    resetControls();
    Serial.println("Forbindelsen er tilbage");
  }
}

bool sendAction(const char *action, const char *direction = nullptr){
  if (!ready || !playerId.length()) return false;
  JsonDocument request, reply;
  identity(request);
  request["action"] = action;
  if (direction) request["params"]["direction"] = direction;
  bool ok = postController("/api/controller/action", request, reply);
  applyGameState(reply);
  return ok;
}
// 00 = scan
// 01 = rotate left
// 02 = move forwards
// 03 = rotate right
// 04 = dig
void handleControls(uint8_t down, uint8_t pressed, uint8_t released){
  if (released & (k1 | k2 | k3)) repeatedKey = -1;
  if (pressed & k4) {sendAction("dig"); return; }
  if (pressed & k0) {sendAction("scan"); return; }
  int key = -1;
  const char *direction = nullptr;
  const char *action = nullptr;
  if ((down & (k2)) == k2) {key = 2; direction = "UP"; action = "move";}
  // else if ((down & (k0 | k2)) == k0) {key = 0; direction = "DOWN";}
  else if ((down & (k1 | k3)) == k1) {key = 1; direction = "LEFT"; action = "rotate";}
  else if ((down & (k1 | k3)) == k3) {key = 3; direction = "RIGHT"; action = "rotate";}
   if (repeatKey(key) && action != nullptr) {
    sendAction(action, direction);
  }
// Serial.println("action:");
// Serial.println(action ? action : "none");
// Serial.println("direction:");
// Serial.println(direction ? direction : "none");
}


void dispatchControls(){
  uint8_t down, pressed, released;
  int event = inputs.next(millis(), down, pressed, released);
  if (event < 0) {
    if (strcmp(GAME_PATH, "/Pong") == 0) sendAction("stop");
    releaseBeforePlay = true;
    resetControls();
    Serial.println("Lang svaretid: slip knapper");
    return;
  }
  handleControls(down, pressed, released);
}

bool repeatKey(int key){
  if (key < 0){
    repeatedKey = -1;
    return false;
  }
  if (key != repeatedKey || millis() - lastRepeat >= REPEAT_MS){
    repeatedKey = key;
    lastRepeat = millis();
    return true;
  }
  return false;
}

void setup() {
  Serial.begin(115200);
  uint32_t started = millis();
  while (!Serial && millis() - started < 1500) { ; }   // just wait briefly for Serial

  inputs.begin();
  transport.onPoll(pollWhileWaiting);
  WiFi.setTimeout(3000);
  transport.setRetry(false);
  lastJoinAttempt = millis() - 5000;
  lastWifiAttempt = millis() - 10000;
  if (USE_CASE) carrier.withCase(); else carrier.noCase();
  if (!carrier.begin()) {
    Serial.println("Carrier failed");
    while (true) {}
  }
}

void loop() {
updateLight();
networkUsed = false;
readKeys();
if (!ensureWifi()) {inputs.clear(); delay(5); return;}
if (!playerId.length()) {
  inputs.clear();
  if (millis() - lastJoinAttempt >= 5000) joinGame();
  }else {
  if (releaseBeforePlay && keys == 0 && rawMask == 0){
    releaseBeforePlay = false;
    inputs.clear();
    resetControls();
  }
  uint32_t sinceHeartbeat = millis() - lastHeartbeat;
  if (sinceHeartbeat >= HEARTBEAT_MS + 500){
    heartbeat();
  }else if (ready && !releaseBeforePlay) {
    dispatchControls();
  }else {
    inputs.clear();
  }
  if (!networkUsed && playerId.length() && millis() - lastHeartbeat >= HEARTBEAT_MS) heartbeat();
  }
delay(3);
}
