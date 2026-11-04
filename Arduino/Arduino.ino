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

ControllerClient transport;
String playerId;
String deviceId;
String lastError;
bool ready = false;
bool networkWasUp = false;
bool releaseBeforePlay = true;
bool networkUsed = false;
uint32_t lastJoinAttempt = 0;
uint32_t lastHeartbeat = 0;
int lastHttpStatus = 0;

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
    Serial.println(packet);
    Serial.println(endpoint);
    Serial.println(payload);
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
  Serial.print("packet");
  Serial.println(packet.c_str());
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
    const char *fields[] = {"ok", "playerId", "error", "message", "side"};
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
  return true;
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
  return postController("/api/controller/action", request, reply);
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
  // testing to see if direction & action gets sendt
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
while (!Serial && millis() - started < 1500) {

  inputs.begin();
  transport.onPoll(readKeys);
  WiFi.setTimeout(3000);
  transport.setRetry(false);
  lastJoinAttempt = millis() - 5000;
  
  lastWifiAttempt = millis() - 10000;
  if (USE_CASE) carrier.withCase();
  else carrier.noCase();
  if (!carrier.begin()){
    Serial.println("Carrier failed");
    while (true){}
  }
  }
}

void loop() {
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
