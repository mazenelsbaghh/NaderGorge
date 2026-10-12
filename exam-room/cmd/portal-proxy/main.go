// portal-proxy exposes only the student service on the default HTTP port used
// by the EAP620 HD standalone portal's promotional URL.
package main

import (
	"flag"
	"log"
	"net"
	"net/http"
	"net/http/httputil"
	"net/url"
	"os"
	"strings"
	"time"
)

func localHost(host string) bool {
	if host == "localhost" || host == "127.0.0.1" {
		return true
	}
	ip := net.ParseIP(host)
	if ip == nil || ip.To4() == nil {
		return false
	}
	addresses, err := net.InterfaceAddrs()
	if err != nil {
		return false
	}
	for _, address := range addresses {
		if network, ok := address.(*net.IPNet); ok && network.IP.Equal(ip) {
			return true
		}
	}
	return false
}

func studentUpstream() *url.URL {
	target, _ := url.Parse("http://127.0.0.1:8765")
	return target
}

func studentProxy(target *url.URL) *httputil.ReverseProxy {
	return &httputil.ReverseProxy{
		Rewrite: func(request *httputil.ProxyRequest) {
			request.SetURL(target)
			request.Out.Host = target.Host
			request.Out.Header.Del("X-Forwarded-For")
			request.Out.Header.Del("X-Forwarded-Host")
			request.Out.Header.Del("X-Forwarded-Proto")
			request.Out.Header.Del("Forwarded")
			request.Out.Header.Del("X-Massar-Client-IP")
			if address, _, err := net.SplitHostPort(request.In.RemoteAddr); err == nil && net.ParseIP(address) != nil {
				request.Out.Header.Set("X-Massar-Client-IP", address)
			}
			request.Out.Header.Del("X-Admin-Token")
			request.Out.Header.Del("Cookie")
			for _, cookie := range request.In.Cookies() {
				if cookie.Name == "massar_student" {
					request.Out.AddCookie(cookie)
				}
			}
			if request.In.Header.Get("Origin") != "" {
				request.Out.Header.Set("Origin", target.String())
			}
		},
		Transport: &http.Transport{
			Proxy:                 nil,
			DialContext:           (&net.Dialer{Timeout: 3 * time.Second}).DialContext,
			MaxIdleConns:          512,
			MaxIdleConnsPerHost:   512,
			IdleConnTimeout:       90 * time.Second,
			ResponseHeaderTimeout: 15 * time.Second,
		},
		ErrorHandler: studentProxyError,
	}
}

func handler() http.Handler {
	proxy := studentProxy(studentUpstream())
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path == "/capport" {
			http.Error(w, "CAPPORT requires its configured HTTPS endpoint", http.StatusNotFound)
			return
		}
		host := r.Host
		if strings.HasSuffix(host, ":80") {
			host = strings.TrimSuffix(host, ":80")
		}
		if !localHost(host) {
			// Phones probe an ordinary HTTP address after joining Wi-Fi. The
			// local DNS server points that probe here, so send its browser to
			// the actual student page without exposing any student API under
			// the probe's foreign hostname.
			if (r.Method == http.MethodGet || r.Method == http.MethodHead) && r.Header.Get("Origin") == "" {
				w.Header().Set("Cache-Control", "no-store")
				http.Redirect(w, r, "http://10.77.0.1/", http.StatusFound)
				return
			}
			http.Error(w, "Invalid local request", http.StatusForbidden)
			return
		}
		if r.Header.Get("Origin") != "" && r.Header.Get("Origin") != "http://"+r.Host && r.Header.Get("Origin") != "http://"+host {
			http.Error(w, "Invalid local request", http.StatusForbidden)
			return
		}
		proxy.ServeHTTP(w, r)
	})
}

func portalServer(address string, handler http.Handler) *http.Server {
	// Interrupted phone connections must not retain sockets or upstream work for
	// the entire exam. These deadlines apply per request, not to the exam session.
	return &http.Server{Addr: address, Handler: handler, ReadHeaderTimeout: 12 * time.Second,
		ReadTimeout: 30 * time.Second, WriteTimeout: 30 * time.Second, IdleTimeout: 30 * time.Second}
}

func main() {
	listen := flag.String("listen", ":80", "local HTTP listener")
	diagnosticPath := flag.String("diagnostic-log", "/var/db/massar-exam-room/portal-events.jsonl", "bounded portal diagnostics")
	var capport capportConfig
	flag.StringVar(&capport.listen, "https-listen", ":443", "local HTTPS listener")
	flag.StringVar(&capport.hostname, "capport-hostname", "", "DNS hostname for the trusted HTTPS CAPPORT endpoint")
	flag.StringVar(&capport.certFile, "capport-cert", "", "PEM certificate chain for CAPPORT")
	flag.StringVar(&capport.keyFile, "capport-key", "", "private key file for CAPPORT")
	checkCapport := flag.Bool("check-capport", false, "validate CAPPORT certificate configuration and exit without opening listeners")
	checkPortal := flag.Bool("check-portal", false, "verify trusted HTTPS discovery and exam service at 10.77.0.1, then exit")
	flag.Parse()
	if *checkCapport && *checkPortal {
		log.Fatal("Choose either --check-capport or --check-portal")
	}
	if *checkPortal {
		if err := writePortalReadiness(capport.hostname, os.Stdout); err != nil {
			log.Fatal(err)
		}
		return
	}
	if *checkCapport {
		if err := writeCapportMetadata(capport, os.Stdout); err != nil {
			log.Fatal(err)
		}
		return
	}
	diagnosticFile, err := openDiagnostics(*diagnosticPath)
	if err != nil {
		log.Fatal(err)
	}
	sink := queueDiagnostics(diagnosticFile)
	server := portalServer(*listen, diagnosticHandler(handler(), sink))
	if capport.configured() {
		httpsServer, err := capportServer(capport, sink)
		if err != nil {
			log.Fatal(err)
		}
		listener, err := net.Listen("tcp", httpsServer.Addr)
		if err != nil {
			log.Fatalf("CAPPORT HTTPS listener: %v", err)
		}
		go func() { log.Fatal(httpsServer.ServeTLS(listener, "", "")) }()
		log.Printf("CAPPORT endpoint: https://%s/capport", capport.hostname)
	}
	log.Printf("Massar student portal: http://%s -> http://127.0.0.1:8765", *listen)
	log.Fatal(server.ListenAndServe())
}
