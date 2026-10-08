package main

import (
	"crypto/tls"
	"crypto/x509"
	"encoding/pem"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestMobileTrustAndTransportExposeOnlyHomeworkRoutes(t *testing.T) {
	upstream := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("X-Massar-Bridge-Secret") != strings.Repeat("fixture-secret-", 4) {
			t.Error("missing bridge authentication")
		}
		if r.Header.Get("X-Massar-Device-ID") != "" || r.Header.Get("Authorization") != "" {
			t.Error("caller supplied authority forwarded")
		}
		if r.Header.Get("X-Massar-Mobile-Token") != "fixture-mobile-token" {
			t.Error("capability lost")
		}
		w.Write([]byte(`{"homework":"fixture"}`))
	}))
	defer upstream.Close()
	gateway, err := newGateway(testConfig(t, upstream.URL))
	if err != nil {
		t.Fatal(err)
	}
	defer gateway.close()
	authority, err := gateway.mobileAuthority()
	if err != nil {
		t.Fatal(err)
	}
	loaded, err := gateway.mobileAuthority()
	if err != nil || loaded.Certificate != authority.Certificate {
		t.Fatal("mobile trust changes on restart")
	}
	cert, err := mobileCertificate(authority, []net.IP{net.ParseIP("127.0.0.1"), net.ParseIP("192.168.137.1")})
	if err != nil {
		t.Fatal(err)
	}
	leaf, err := x509.ParseCertificate(cert.Certificate[0])
	if err != nil {
		t.Fatal(err)
	}
	if err = leaf.VerifyHostname("192.168.137.1"); err != nil {
		t.Fatal("hotspot address missing from certificate")
	}
	server := httptest.NewUnstartedServer(gateway.mobileHandler(authority))
	server.TLS = &tls.Config{Certificates: []tls.Certificate{cert}}
	server.StartTLS()
	defer server.Close()
	roots := x509.NewCertPool()
	roots.AppendCertsFromPEM([]byte(authority.Certificate))
	client := &http.Client{Transport: &http.Transport{TLSClientConfig: &tls.Config{RootCAs: roots}}}
	defer client.CloseIdleConnections()
	call, _ := http.NewRequest("GET", server.URL+"/mobile/context", nil)
	call.Header.Set("X-Massar-Mobile-Token", "fixture-mobile-token")
	call.Header.Set("X-Massar-Device-ID", "forged-device")
	call.Header.Set("Authorization", "Bearer forged-token")
	reply, err := client.Do(call)
	if err != nil {
		t.Fatal(err)
	}
	body, _ := io.ReadAll(reply.Body)
	reply.Body.Close()
	if reply.StatusCode != 200 || !strings.Contains(string(body), "fixture") {
		t.Fatal("trusted mobile request failed")
	}
	for _, path := range []string{"/api/state", "/control/devices"} {
		reply, err = client.Get(server.URL + path)
		if err != nil {
			t.Fatal(err)
		}
		reply.Body.Close()
		if reply.StatusCode != 404 {
			t.Fatal("general API exposed on mobile listener")
		}
	}
	reply, err = client.Get(server.URL + "/mobile/ca.crt")
	if err != nil {
		t.Fatal(err)
	}
	rootDER, _ := io.ReadAll(reply.Body)
	reply.Body.Close()
	block, _ := pem.Decode([]byte(authority.Certificate))
	if string(rootDER) != string(block.Bytes) {
		t.Fatal("wrong trust certificate exported")
	}
}
