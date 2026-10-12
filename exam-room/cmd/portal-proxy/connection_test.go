package main

import (
	"bufio"
	"fmt"
	"io"
	"net"
	"net/http"
	"testing"
	"time"
)

func startBoundedPortal(t *testing.T, handler http.Handler) (*http.Server, string) {
	t.Helper()
	server := portalServer("127.0.0.1:0", handler)
	// Keep the production choice of enabled/disabled deadlines while shortening
	// enabled deadlines so the real socket regressions finish promptly.
	for _, timeout := range []*time.Duration{&server.ReadHeaderTimeout, &server.ReadTimeout, &server.WriteTimeout, &server.IdleTimeout} {
		if *timeout > 0 {
			*timeout = 100 * time.Millisecond
		}
	}
	listener, err := net.Listen("tcp", server.Addr)
	if err != nil {
		t.Fatal(err)
	}
	go server.Serve(listener)
	t.Cleanup(func() { server.Close() })
	return server, listener.Addr().String()
}

func portalConnection(t *testing.T, address string) net.Conn {
	t.Helper()
	connection, err := net.DialTimeout("tcp", address, time.Second)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { connection.Close() })
	connection.SetDeadline(time.Now().Add(time.Second))
	return connection
}

func TestIdleCaptiveProbeConnectionIsReleased(t *testing.T) {
	_, address := startBoundedPortal(t, handler())
	connection := portalConnection(t, address)
	fmt.Fprint(connection, "GET /generate_204 HTTP/1.1\r\nHost: connectivitycheck.gstatic.com\r\n\r\n")
	reader := bufio.NewReader(connection)
	response, err := http.ReadResponse(reader, nil)
	if err != nil {
		t.Fatal(err)
	}
	io.Copy(io.Discard, response.Body)
	response.Body.Close()
	if response.StatusCode != http.StatusFound || response.Header.Get("Location") != "http://10.77.0.1/" {
		t.Fatal("captive probe did not return its local redirect")
	}
	if _, err := reader.ReadByte(); err != io.EOF {
		t.Fatalf("idle probe socket was not released: %v", err)
	}
}

func TestIncompleteStudentRequestBodyIsReleased(t *testing.T) {
	finished := make(chan error, 1)
	_, address := startBoundedPortal(t, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		_, err := io.ReadAll(r.Body)
		finished <- err
	}))
	connection := portalConnection(t, address)
	fmt.Fprint(connection, "POST /api/answers HTTP/1.1\r\nHost: localhost\r\nContent-Length: 100\r\n\r\n{")
	select {
	case err := <-finished:
		if err == nil {
			t.Fatal("incomplete answer body was treated as complete")
		}
	case <-time.After(time.Second):
		t.Fatal("partial answer body kept its connection and handler indefinitely")
	}
}

func TestStudentThatStopsReadingCannotHoldPortalWriter(t *testing.T) {
	finished := make(chan error, 1)
	_, address := startBoundedPortal(t, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		chunk := make([]byte, 128*1024)
		for index := 0; index < 128; index++ {
			if _, err := w.Write(chunk); err != nil {
				finished <- err
				return
			}
		}
		finished <- nil
	}))
	connection := portalConnection(t, address)
	connection.(*net.TCPConn).SetReadBuffer(1024)
	fmt.Fprint(connection, "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n")
	select {
	case err := <-finished:
		if err == nil {
			t.Fatal("test client unexpectedly consumed the oversized response")
		}
	case <-time.After(time.Second):
		t.Fatal("non-reading student kept its connection and writer indefinitely")
	}
}
