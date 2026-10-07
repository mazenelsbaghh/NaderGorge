package main

import (
	"bytes"
	"crypto/sha256"
	"crypto/tls"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

type runningGateway struct {
	gateway *Gateway
	ready   Ready
	client  *http.Client
	url     string
}

func startFixture(t *testing.T, config Config) *runningGateway {
	t.Helper()
	gateway, err := newGateway(config)
	if err != nil {
		t.Fatal(err)
	}
	ready, err := gateway.start()
	if err != nil {
		t.Fatal(err)
	}
	client := pinnedClient(ready.CertificateSHA256)
	fixture := &runningGateway{gateway, ready, client, "https://127.0.0.1:" + strconv.Itoa(ready.Port)}
	t.Cleanup(func() { client.CloseIdleConnections(); gateway.close() })
	return fixture
}

func testConfig(t *testing.T, upstream string) Config {
	t.Helper()
	zero := 0
	return Config{Upstream: upstream, UpstreamSecret: strings.Repeat("fixture-secret-", 4), DataDir: t.TempDir(), Name: "سنتر نادر جورج", Port: &zero, DiscoveryPort: &zero}
}

func pinnedClient(pin string) *http.Client {
	return &http.Client{Timeout: 5 * time.Second, Transport: &http.Transport{Proxy: nil, TLSClientConfig: &tls.Config{
		// A self-signed LAN certificate is trusted only by the explicitly paired DER fingerprint.
		InsecureSkipVerify: true,
		VerifyConnection: func(state tls.ConnectionState) error {
			digest := sha256.Sum256(state.PeerCertificates[0].Raw)
			if hex.EncodeToString(digest[:]) != pin {
				return errors.New("certificate pin mismatch")
			}
			return nil
		},
	}}}
}

func (f *runningGateway) request(t *testing.T, method, path, token string, body any, headers http.Header) (int, []byte) {
	t.Helper()
	var input io.Reader
	switch value := body.(type) {
	case string:
		input = strings.NewReader(value)
	case []byte:
		input = bytes.NewReader(value)
	case nil:
	default:
		encoded, err := json.Marshal(value)
		if err != nil {
			t.Fatal(err)
		}
		input = bytes.NewReader(encoded)
	}
	request, err := http.NewRequest(method, f.url+path, input)
	if err != nil {
		t.Fatal(err)
	}
	request.Header = headers.Clone()
	if request.Header == nil {
		request.Header = make(http.Header)
	}
	if token != "" {
		request.Header.Set("Authorization", "Bearer "+token)
	}
	response, err := f.client.Do(request)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	result, err := io.ReadAll(response.Body)
	if err != nil {
		t.Fatal(err)
	}
	return response.StatusCode, result
}

func requireStatus(t *testing.T, got, expected int) {
	t.Helper()
	if got != expected {
		t.Fatalf("HTTP status %d, want %d", got, expected)
	}
}

func (f *runningGateway) pair(t *testing.T, id string) string {
	t.Helper()
	status, result := f.request(t, "POST", "/pair", "", PairRequest{f.gateway.pairing.currentCode().Code, id, "جهاز الاستقبال"}, nil)
	requireStatus(t, status, http.StatusOK)
	var response struct {
		Token    string
		DeviceID string
		HostID   string
		Protocol int
	}
	if err := json.Unmarshal(result, &response); err != nil {
		t.Fatal(err)
	}
	if len(response.Token) != 43 || response.DeviceID != id || response.HostID != f.ready.HostID || response.Protocol != 1 {
		t.Fatal("invalid pair identity or credential")
	}
	return response.Token
}

func TestTLSProxyUsesVerifiedDeviceAndOpaqueStaffSession(t *testing.T) {
	received := make(chan struct {
		path, body string
		headers    http.Header
	}, 1)
	upstream := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		body, _ := io.ReadAll(r.Body)
		received <- struct {
			path, body string
			headers    http.Header
		}{r.URL.RequestURI(), string(body), r.Header.Clone()}
		w.Header().Set("X-Massar-Bridge-Secret", "must-not-escape")
		w.WriteHeader(http.StatusCreated)
		io.WriteString(w, `{"committed":true}`)
	}))
	defer upstream.Close()
	config := testConfig(t, upstream.URL)
	f := startFixture(t, config)
	status, _ := f.request(t, "POST", "/api/command", "", "{}", nil)
	requireStatus(t, status, http.StatusUnauthorized)
	token := f.pair(t, "desk-2")
	headers := http.Header{"X-Massar-State-Patch": {"1"}, "X-Massar-State-Version": {"snapshot-version"}, "X-Massar-Session": {"opaque-staff-session"}, "X-Massar-Actor": {"forged-admin"}, "X-Massar-Bridge-Secret": {"forged"}, "X-Massar-Device-Id": {"forged-device"}}
	requestBody := `{"commandId":"receipt-1","operation":"collectEntry"}`
	status, body := f.request(t, "POST", "/api/command?retry=1", token, requestBody, headers)
	requireStatus(t, status, http.StatusCreated)
	if string(body) != `{"committed":true}` {
		t.Fatal("proxy changed command response")
	}
	actual := <-received
	if actual.path != "/api/command?retry=1" || actual.body != requestBody {
		t.Fatal("proxy changed command target or payload")
	}
	if actual.headers.Get("Authorization") != "" || actual.headers.Get("X-Massar-Actor") != "" || actual.headers.Get("X-Massar-Bridge-Secret") != config.UpstreamSecret || actual.headers.Get("X-Massar-Device-ID") != "desk-2" || actual.headers.Get("X-Massar-Session") != "opaque-staff-session" || actual.headers.Get("X-Massar-State-Version") != "snapshot-version" || actual.headers.Get("X-Massar-State-Patch") != "1" {
		t.Fatal("untrusted identity or pairing credential reached bridge")
	}
	select {
	case <-received:
		t.Fatal("unpaired request reached bridge")
	default:
	}
	wrongPin := pinnedClient(strings.Repeat("0", 64))
	defer wrongPin.CloseIdleConnections()
	if response, err := wrongPin.Get(f.url + "/health"); err == nil {
		response.Body.Close()
		t.Fatal("wrong TLS certificate pin accepted")
	}
}

func TestRevocationAndPairReplacementPersistAcrossRestart(t *testing.T) {
	upstream := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { io.WriteString(w, `{"ok":true}`) }))
	defer upstream.Close()
	config := testConfig(t, upstream.URL)
	f := startFixture(t, config)
	oldToken := f.pair(t, "desk-2")
	currentToken := f.pair(t, "desk-2")
	status, _ := f.request(t, "GET", "/api/state", oldToken, nil, nil)
	requireStatus(t, status, 401)
	status, _ = f.request(t, "GET", "/api/state", currentToken, nil, nil)
	requireStatus(t, status, 200)
	contents, err := os.ReadFile(filepath.Join(config.DataDir, "devices.json"))
	if err != nil {
		t.Fatal(err)
	}
	if bytes.Contains(contents, []byte(currentToken)) || bytes.Contains(contents, []byte(config.UpstreamSecret)) || !bytes.Contains(contents, []byte("tokenHash")) {
		t.Fatal("device registry leaks plaintext credentials")
	}
	if err = f.gateway.close(); err != nil {
		t.Fatal(err)
	}
	f2 := startFixture(t, config)
	if f2.ready.HostID != f.ready.HostID || f2.ready.CertificateSHA256 != f.ready.CertificateSHA256 {
		t.Fatal("host identity changed after restart")
	}
	status, _ = f2.request(t, "GET", "/api/state", currentToken, nil, nil)
	requireStatus(t, status, 200)
	status, _ = f2.request(t, "POST", "/control/devices/revoke", currentToken, map[string]string{"deviceId": "desk-2"}, nil)
	requireStatus(t, status, 403)
	status, _ = f2.request(t, "POST", "/control/devices/revoke", config.UpstreamSecret, map[string]string{"deviceId": "desk-2"}, nil)
	requireStatus(t, status, 200)
	status, _ = f2.request(t, "GET", "/api/state", currentToken, nil, nil)
	requireStatus(t, status, 401)
	f2.gateway.close()
	f3 := startFixture(t, config)
	status, _ = f3.request(t, "GET", "/api/state", currentToken, nil, nil)
	requireStatus(t, status, 401)
	status, devices := f3.request(t, "GET", "/control/devices", config.UpstreamSecret, nil, nil)
	requireStatus(t, status, 200)
	var listed struct{ Devices []Device }
	if err = json.Unmarshal(devices, &listed); err != nil {
		t.Fatal(err)
	}
	if len(listed.Devices) != 1 || !listed.Devices[0].Revoked || bytes.Contains(devices, []byte("tokenHash")) {
		t.Fatal("revoked device inventory is incorrect or exposes token hashes")
	}
}

func TestPairAttemptsBlockUntilOwnerRotatesAndExpiredCodesReject(t *testing.T) {
	f := startFixture(t, testConfig(t, "http://127.0.0.1:1"))
	wrong := "000000"
	if f.ready.PairingCode == wrong {
		wrong = "000001"
	}
	for i := 0; i < 5; i++ {
		status, _ := f.request(t, "POST", "/pair", "", PairRequest{wrong, "desk-2", "استقبال"}, nil)
		requireStatus(t, status, 403)
	}
	status, _ := f.request(t, "POST", "/pair", "", PairRequest{f.ready.PairingCode, "desk-2", "استقبال"}, nil)
	requireStatus(t, status, 429)
	status, body := f.request(t, "POST", "/control/pairing", f.gateway.config.UpstreamSecret, nil, nil)
	requireStatus(t, status, 200)
	var code PairingCode
	if err := json.Unmarshal(body, &code); err != nil {
		t.Fatal(err)
	}
	if len(code.Code) != 6 || !code.ExpiresAt.After(time.Now()) {
		t.Fatal("rotation did not create valid timed code")
	}
	f.pair(t, "desk-2")
	f.gateway.pairing.mu.Lock()
	f.gateway.pairing.expires = time.Now().Add(-time.Second)
	f.gateway.pairing.mu.Unlock()
	status, _ = f.request(t, "POST", "/pair", "", PairRequest{code.Code, "desk-3", "استقبال"}, nil)
	requireStatus(t, status, 403)
}

func TestDiscoveryPublishesOnlyMatchingProtocolAndPinnedIdentity(t *testing.T) {
	f := startFixture(t, testConfig(t, "http://127.0.0.1:1"))
	peer, err := net.DialUDP("udp4", nil, &net.UDPAddr{IP: net.ParseIP("127.0.0.1"), Port: f.gateway.discovery.LocalAddr().(*net.UDPAddr).Port})
	if err != nil {
		t.Fatal(err)
	}
	defer peer.Close()
	for _, packet := range []string{`{"kind":"massar-discover","protocol":2}`, `{"kind":"other","protocol":1}`, strings.Repeat("x", 1024)} {
		if _, err = peer.Write([]byte(packet)); err != nil {
			t.Fatal(err)
		}
		peer.SetReadDeadline(time.Now().Add(150 * time.Millisecond))
		_, err = peer.Read(make([]byte, 1024))
		if timeout, ok := err.(net.Error); !ok || !timeout.Timeout() {
			t.Fatal("invalid discovery request received response")
		}
	}
	peer.SetReadDeadline(time.Now().Add(time.Second))
	peer.Write([]byte(`{"kind":"massar-discover","protocol":1}`))
	buffer := make([]byte, 1024)
	count, err := peer.Read(buffer)
	if err != nil {
		t.Fatal(err)
	}
	var host HostDescription
	if err = json.Unmarshal(buffer[:count], &host); err != nil {
		t.Fatal(err)
	}
	if host != f.gateway.description() || bytes.Contains(buffer[:count], []byte("pairingCode")) {
		t.Fatal("discovery did not match TLS host identity")
	}
	status, body := f.request(t, "GET", "/health", "", nil, nil)
	requireStatus(t, status, 200)
	var health HostDescription
	if err = json.Unmarshal(body, &health); err != nil {
		t.Fatal(err)
	}
	if health != host {
		t.Fatal("health identity differs from discovery")
	}
}

func TestPayloadAndSessionHeaderLimitsRejectBeforeForwarding(t *testing.T) {
	var calls atomic.Int32
	upstream := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		io.Copy(io.Discard, r.Body)
		io.WriteString(w, "{}")
	}))
	defer upstream.Close()
	f := startFixture(t, testConfig(t, upstream.URL))
	token := f.pair(t, "desk-2")
	cases := []struct {
		name    string
		body    string
		headers http.Header
		status  int
	}{
		{"oversized", strings.Repeat("x", maxAPIBody+1), nil, 413},
		{"duplicate session", "{}", http.Header{"X-Massar-Session": {"first", "second"}}, 400},
		{"oversized session", "{}", http.Header{"X-Massar-Session": {strings.Repeat("s", 4097)}}, 400},
		{"duplicate state version", "{}", http.Header{"X-Massar-State-Version": {"first", "second"}}, 400},
		{"oversized state version", "{}", http.Header{"X-Massar-State-Version": {strings.Repeat("s", 129)}}, 400},
		{"duplicate state patch", "{}", http.Header{"X-Massar-State-Patch": {"1", "1"}}, 400},
		{"oversized state patch", "{}", http.Header{"X-Massar-State-Patch": {strings.Repeat("s", 17)}}, 400},
		{"upgrade", "{}", http.Header{"Upgrade": {"websocket"}}, 400},
	}
	for _, test := range cases {
		t.Run(test.name, func(t *testing.T) {
			status, _ := f.request(t, "POST", "/api/command", token, test.body, test.headers)
			requireStatus(t, status, test.status)
		})
	}
	if calls.Load() != 0 {
		t.Fatal("rejected payload reached authoritative bridge")
	}
	status, _ := f.request(t, "POST", "/api/command", token, strings.Repeat("x", maxAPIBody), nil)
	requireStatus(t, status, 200)
	if calls.Load() != 1 {
		t.Fatal("valid bounded payload did not reach bridge exactly once")
	}
}

func TestMalformedPairRequestsNeverRegisterDevice(t *testing.T) {
	f := startFixture(t, testConfig(t, "http://127.0.0.1:1"))
	for _, body := range []string{`{"code":"` + f.ready.PairingCode + `","deviceId":"desk-2","name":"desk","actor":"admin"}`, `{} {}`, strings.Repeat(" ", 4097) + `{}`} {
		status, _ := f.request(t, "POST", "/pair", "", body, nil)
		requireStatus(t, status, 400)
	}
	if len(f.gateway.pairing.list()) != 0 {
		t.Fatal("malformed pair request registered a device")
	}
}

func TestPairPersistenceFailureDoesNotReplaceActiveCredential(t *testing.T) {
	f := startFixture(t, testConfig(t, "http://127.0.0.1:1"))
	token := f.pair(t, "desk-2")
	path := filepath.Join(f.gateway.config.DataDir, "devices.json")
	if err := os.Rename(path, path+".saved"); err != nil {
		t.Fatal(err)
	}
	if err := os.Mkdir(path, 0700); err != nil {
		t.Fatal(err)
	}
	status, _ := f.request(t, "POST", "/pair", "", PairRequest{f.ready.PairingCode, "desk-2", "replacement"}, nil)
	requireStatus(t, status, 500)
	if _, valid := f.gateway.pairing.authenticatedDevice(token); !valid {
		t.Fatal("failed disk write invalidated active credential")
	}
	status, _ = f.request(t, "POST", "/control/devices/revoke", f.gateway.config.UpstreamSecret, map[string]string{"deviceId": "desk-2"}, nil)
	requireStatus(t, status, 500)
	if _, valid := f.gateway.pairing.authenticatedDevice(token); !valid {
		t.Fatal("failed revoke disk write changed active credential")
	}
	files, err := filepath.Glob(filepath.Join(f.gateway.config.DataDir, ".massar-*.tmp"))
	if err != nil || len(files) != 0 {
		t.Fatal("failed disk write leaked staging files")
	}
}

func TestProxyRedirectAndDisconnectedHostAreSafeFailures(t *testing.T) {
	var externalCalls atomic.Int32
	external := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { externalCalls.Add(1) }))
	defer external.Close()
	upstream := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { http.Redirect(w, r, external.URL, 307) }))
	f := startFixture(t, testConfig(t, upstream.URL))
	token := f.pair(t, "desk-2")
	status, body := f.request(t, "POST", "/api/command", token, "{}", nil)
	requireStatus(t, status, 502)
	if string(body) != "{\"error\":\"host_unavailable\"}\n" || externalCalls.Load() != 0 {
		t.Fatal("redirect leaked request or unsafe upstream error")
	}
	upstream.Close()
	status, body = f.request(t, "GET", "/api/state", token, nil, nil)
	requireStatus(t, status, 502)
	if strings.Contains(string(body), f.gateway.config.UpstreamSecret) || strings.Contains(string(body), upstream.URL) {
		t.Fatal("disconnected host response exposed internals")
	}
}

func TestOwnerControlsRejectForwardedLoopbackSpoof(t *testing.T) {
	gateway, err := newGateway(testConfig(t, "http://127.0.0.1:1"))
	if err != nil {
		t.Fatal(err)
	}
	request := httptest.NewRequest("GET", "https://host/control/devices", nil)
	request.RemoteAddr = "192.168.1.42:50200"
	request.Header.Set("Authorization", "Bearer "+gateway.config.UpstreamSecret)
	request.Header.Set("X-Forwarded-For", "127.0.0.1")
	response := httptest.NewRecorder()
	gateway.server.Handler.ServeHTTP(response, request)
	requireStatus(t, response.Code, 403)
}
