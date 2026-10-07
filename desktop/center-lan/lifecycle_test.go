package main

import (
	"encoding/json"
	"io"
	"net"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"testing"
	"time"
)

// Regression: a terminated Flutter parent previously left the child and its LAN ports running.
func TestParentPipeEOFStopsActualGatewayAndReleasesBothListeners(t *testing.T) {
	executable := filepath.Join(t.TempDir(), "massar-lan-host")
	if runtime.GOOS == "windows" {
		executable += ".exe"
	}
	build := exec.Command("go", "build", "-o", executable, ".")
	if output, err := build.CombinedOutput(); err != nil {
		t.Fatalf("build actual gateway: %v\n%s", err, output)
	}
	command := exec.Command(executable)
	stdin, err := command.StdinPipe()
	if err != nil {
		t.Fatal(err)
	}
	stdout, err := command.StdoutPipe()
	if err != nil {
		t.Fatal(err)
	}
	command.Stderr = io.Discard
	if err = command.Start(); err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { stdin.Close(); command.Process.Kill() })
	config := testConfig(t, "http://127.0.0.1:1")
	// Use a known temporary UDP port to prove that EOF releases discovery as well as HTTPS.
	reservation, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4zero})
	if err != nil {
		t.Fatal(err)
	}
	udpPort := reservation.LocalAddr().(*net.UDPAddr).Port
	reservation.Close()
	config.DiscoveryPort = &udpPort
	if err = json.NewEncoder(stdin).Encode(config); err != nil {
		t.Fatal(err)
	}
	result := make(chan struct {
		ready Ready
		err   error
	}, 1)
	go func() {
		var ready Ready
		err := json.NewDecoder(stdout).Decode(&ready)
		result <- struct {
			ready Ready
			err   error
		}{ready, err}
	}()
	var ready Ready
	select {
	case started := <-result:
		if started.err != nil || !started.ready.Ready {
			t.Fatal("actual child did not become ready")
		}
		ready = started.ready
	case <-time.After(10 * time.Second):
		t.Fatal("actual child startup timed out")
	}
	client := pinnedClient(ready.CertificateSHA256)
	defer client.CloseIdleConnections()
	response, err := client.Get("https://127.0.0.1:" + strconv.Itoa(ready.Port) + "/health")
	if err != nil {
		t.Fatal(err)
	}
	response.Body.Close()
	requireStatus(t, response.StatusCode, 200)
	if err = stdin.Close(); err != nil {
		t.Fatal(err)
	}
	exited := make(chan error, 1)
	go func() { exited <- command.Wait() }()
	select {
	case err = <-exited:
		if err != nil {
			t.Fatal("child exited unsuccessfully after parent pipe EOF")
		}
	case <-time.After(7 * time.Second):
		t.Fatal("orphaned child survived parent pipe EOF")
	}
	listener, err := net.Listen("tcp4", net.JoinHostPort("0.0.0.0", strconv.Itoa(ready.Port)))
	if err != nil {
		t.Fatal("child still holds HTTPS listener after EOF")
	}
	listener.Close()
	discovery, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4zero, Port: udpPort})
	if err != nil {
		t.Fatal("child still holds discovery listener after EOF")
	}
	discovery.Close()
}
