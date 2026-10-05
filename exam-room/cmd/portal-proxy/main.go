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

func handler() http.Handler {
	target, _ := url.Parse("http://127.0.0.1:8765")
	proxy := &httputil.ReverseProxy{
		Rewrite: func(request *httputil.ProxyRequest) {
			request.SetURL(target)
			request.Out.Host = target.Host
			request.Out.Header.Del("X-Forwarded-For")
			request.Out.Header.Del("X-Forwarded-Host")
			request.Out.Header.Del("X-Forwarded-Proto")
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
	}
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
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

func main() {
	listen := flag.String("listen", ":80", "local HTTP listener")
	flag.Parse()
	server := &http.Server{Addr: *listen, Handler: handler(), ReadHeaderTimeout: 12 * time.Second}
	log.Printf("Massar student portal: http://%s -> http://127.0.0.1:8765", *listen)
	log.Fatal(server.ListenAndServe())
}
