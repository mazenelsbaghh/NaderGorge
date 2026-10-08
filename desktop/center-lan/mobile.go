package main

import (
	"context"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/tls"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/json"
	"encoding/pem"
	"errors"
	"io"
	"log"
	"math/big"
	"net"
	"net/http"
	"net/http/httputil"
	"os"
	"path/filepath"
	"strings"
	"time"
)

// A separate CA keeps browser trust setup from changing paired desktop pins.
func (g *Gateway) mobileAuthority() (savedIdentity, error) {
	path := filepath.Join(g.config.DataDir, "mobile-authority.json")
	contents, err := os.ReadFile(path)
	if err == nil {
		var saved savedIdentity
		err = json.Unmarshal(contents, &saved)
		return saved, err
	}
	if !errors.Is(err, os.ErrNotExist) {
		return savedIdentity{}, err
	}
	key, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		return savedIdentity{}, err
	}
	serial, err := rand.Int(rand.Reader, new(big.Int).Lsh(big.NewInt(1), 128))
	if err != nil {
		return savedIdentity{}, err
	}
	cert := &x509.Certificate{SerialNumber: serial, Subject: pkix.Name{CommonName: "Massar Mobile " + g.identity.HostID}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().AddDate(5, 0, 0), IsCA: true, BasicConstraintsValid: true, KeyUsage: x509.KeyUsageCertSign | x509.KeyUsageDigitalSignature}
	der, err := x509.CreateCertificate(rand.Reader, cert, cert, &key.PublicKey, key)
	if err != nil {
		return savedIdentity{}, err
	}
	encodedKey, err := x509.MarshalPKCS8PrivateKey(key)
	if err != nil {
		return savedIdentity{}, err
	}
	saved := savedIdentity{Certificate: string(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der})), PrivateKey: string(pem.EncodeToMemory(&pem.Block{Type: "PRIVATE KEY", Bytes: encodedKey}))}
	return saved, persistJSON(path, saved)
}

func mobileCertificate(authority savedIdentity, addresses []net.IP) (tls.Certificate, error) {
	pair, err := tls.X509KeyPair([]byte(authority.Certificate), []byte(authority.PrivateKey))
	if err != nil {
		return tls.Certificate{}, err
	}
	ca, err := x509.ParseCertificate(pair.Certificate[0])
	if err != nil {
		return tls.Certificate{}, err
	}
	if !time.Now().Before(ca.NotAfter) {
		return tls.Certificate{}, errors.New("mobile authority expired")
	}
	key, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		return tls.Certificate{}, err
	}
	serial, err := rand.Int(rand.Reader, new(big.Int).Lsh(big.NewInt(1), 128))
	if err != nil {
		return tls.Certificate{}, err
	}
	cert := &x509.Certificate{SerialNumber: serial, Subject: pkix.Name{CommonName: "Massar Homework"}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().AddDate(0, 3, 0), KeyUsage: x509.KeyUsageDigitalSignature, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}, IPAddresses: addresses, DNSNames: []string{"localhost"}}
	der, err := x509.CreateCertificate(rand.Reader, cert, ca, &key.PublicKey, pair.PrivateKey)
	return tls.Certificate{Certificate: [][]byte{der, pair.Certificate[0]}, PrivateKey: key}, err
}

func mobileAddresses() ([]net.IP, error) {
	interfaces, err := net.InterfaceAddrs()
	if err != nil {
		return nil, err
	}
	addresses := []net.IP{net.IPv4(127, 0, 0, 1)}
	for _, address := range interfaces {
		ip, _, err := net.ParseCIDR(address.String())
		if err == nil && ip.To4() != nil && !ip.IsLoopback() && ip.IsPrivate() {
			addresses = append(addresses, ip)
		}
	}
	return addresses, nil
}

func (g *Gateway) mobileHandler(authority savedIdentity) http.Handler {
	upstream, _ := g.config.upstreamURL()
	proxy := &httputil.ReverseProxy{Transport: g.transport, Rewrite: func(request *httputil.ProxyRequest) {
		request.SetURL(upstream)
		request.Out.Host = upstream.Host
		token := request.In.Header.Get("X-Massar-Mobile-Token")
		request.Out.Header = http.Header{}
		request.Out.Header.Set("Content-Type", request.In.Header.Get("Content-Type"))
		request.Out.Header.Set("X-Massar-Bridge-Secret", g.config.UpstreamSecret)
		request.Out.Header.Set("X-Massar-Mobile-Token", token)
	}, ErrorHandler: func(w http.ResponseWriter, r *http.Request, err error) {
		writeError(w, http.StatusBadGateway, "host_unavailable")
	}, ErrorLog: log.New(io.Discard, "", 0)}
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Cache-Control", "no-store")
		w.Header().Set("Referrer-Policy", "no-referrer")
		w.Header().Set("X-Content-Type-Options", "nosniff")
		if r.URL.Path == "/mobile/ca.crt" && r.Method == "GET" {
			block, _ := pem.Decode([]byte(authority.Certificate))
			w.Header().Set("Content-Type", "application/x-x509-ca-cert")
			w.Header().Set("Content-Disposition", `attachment; filename="massar-mobile.crt"`)
			w.Write(block.Bytes)
			return
		}
		if !strings.HasPrefix(r.URL.Path, "/mobile/") || r.Header.Get("Upgrade") != "" || !boundedHeader(r.Header, "X-Massar-Mobile-Token", 128) {
			http.NotFound(w, r)
			return
		}
		if r.Method != "GET" && r.Method != "POST" {
			w.WriteHeader(http.StatusMethodNotAllowed)
			return
		}
		r.Body = http.MaxBytesReader(w, r.Body, 4096)
		proxy.ServeHTTP(w, r)
	})
}

func (g *Gateway) startMobile(w http.ResponseWriter, r *http.Request) {
	if !g.owner(w, r) {
		return
	}
	g.mobileMu.Lock()
	defer g.mobileMu.Unlock()
	if g.mobile != nil {
		g.mobile.Close()
		g.mobile = nil
	}
	addresses, err := mobileAddresses()
	if err != nil {
		writeError(w, 500, "mobile_addresses_failed")
		return
	}
	authority, err := g.mobileAuthority()
	if err != nil {
		writeError(w, 500, "mobile_certificate_failed")
		return
	}
	cert, err := mobileCertificate(authority, addresses)
	if err != nil {
		writeError(w, 500, "mobile_certificate_failed")
		return
	}
	listener, err := net.Listen("tcp4", "0.0.0.0:43875")
	if err != nil {
		writeError(w, 500, "mobile_port_unavailable")
		return
	}
	server := &http.Server{Handler: g.mobileHandler(authority), TLSConfig: &tls.Config{MinVersion: tls.VersionTLS12, Certificates: []tls.Certificate{cert}}, ReadHeaderTimeout: 5 * time.Second, ReadTimeout: 20 * time.Second, WriteTimeout: 30 * time.Second, IdleTimeout: 30 * time.Second, MaxHeaderBytes: 8192, ErrorLog: log.New(io.Discard, "", 0)}
	g.mobile = server
	go server.ServeTLS(listener, "", "")
	urls := []string{}
	for _, ip := range addresses {
		if !ip.IsLoopback() {
			urls = append(urls, "https://"+net.JoinHostPort(ip.String(), "43875")+"/mobile/")
		}
	}
	writeJSON(w, 200, map[string]any{"urls": urls})
}

func (g *Gateway) closeMobile() {
	g.mobileMu.Lock()
	defer g.mobileMu.Unlock()
	if g.mobile == nil {
		return
	}
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()
	g.mobile.Shutdown(ctx)
	g.mobile.Close()
	g.mobile = nil
}
func (g *Gateway) stopMobile(w http.ResponseWriter, r *http.Request) {
	if !g.owner(w, r) {
		return
	}
	g.closeMobile()
	writeJSON(w, 200, map[string]bool{"stopped": true})
}
