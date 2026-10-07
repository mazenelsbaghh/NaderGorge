package main

import (
	"encoding/json"
	"errors"
	"io"
	"net"
	"net/url"
	"strconv"
	"strings"
)

const protocol = 1
const defaultPort = 43873
const defaultDiscoveryPort = 43874
const maxAPIBody = 2 << 20

type Config struct {
	Upstream       string `json:"upstream"`
	UpstreamSecret string `json:"upstreamSecret"`
	DataDir        string `json:"dataDir"`
	Name           string `json:"name"`
	Port           *int   `json:"port,omitempty"`
	DiscoveryPort  *int   `json:"discoveryPort,omitempty"`
}

func readConfig(reader io.Reader) (Config, error) {
	var config Config
	decoder := json.NewDecoder(io.LimitReader(reader, 64<<10))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&config); err != nil {
		return config, errors.New("invalid configuration JSON")
	}
	return config, nil
}

func (c Config) upstreamURL() (*url.URL, error) {
	upstream, err := url.Parse(c.Upstream)
	if err != nil || upstream.Scheme != "http" || upstream.User != nil || upstream.RawQuery != "" || upstream.Fragment != "" || (upstream.Path != "" && upstream.Path != "/") {
		return nil, errors.New("upstream must be a loopback HTTP origin")
	}
	ip := net.ParseIP(upstream.Hostname())
	port, err := strconv.Atoi(upstream.Port())
	if ip == nil || !ip.IsLoopback() || err != nil || port < 1 || port > 65535 {
		return nil, errors.New("upstream must use a loopback IP and explicit port")
	}
	return upstream, nil
}

func (c Config) validate() error {
	if _, err := c.upstreamURL(); err != nil {
		return err
	}
	if len(c.UpstreamSecret) < 32 || len(c.UpstreamSecret) > 512 || strings.ContainsAny(c.UpstreamSecret, "\r\n") {
		return errors.New("invalid upstream secret")
	}
	if strings.TrimSpace(c.DataDir) == "" || !validName(c.Name) {
		return errors.New("invalid host directory or name")
	}
	for _, port := range []*int{c.Port, c.DiscoveryPort} {
		if port != nil && (*port < 0 || *port > 65535) {
			return errors.New("invalid listener port")
		}
	}
	return nil
}

func configuredPort(port *int, fallback int) int {
	if port != nil {
		return *port
	}
	return fallback
}

func validName(name string) bool {
	return name == strings.TrimSpace(name) && len([]rune(name)) > 0 && len([]rune(name)) <= 120 && !strings.ContainsAny(name, "\r\n\x00")
}
