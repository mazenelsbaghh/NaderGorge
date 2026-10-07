package main

import (
	"encoding/json"
	"strconv"
	"time"
)

func portText(port int) string { return strconv.Itoa(port) }

func (g *Gateway) serveDiscovery() {
	packet := make([]byte, 512)
	window := time.Now()
	responses := 0
	for {
		count, peer, err := g.discovery.ReadFromUDP(packet)
		if err != nil {
			return
		}
		if time.Since(window) >= time.Second {
			window = time.Now()
			responses = 0
		}
		if responses >= 50 || count == len(packet) || !(peer.IP.IsPrivate() || peer.IP.IsLoopback() || peer.IP.IsLinkLocalUnicast()) {
			continue
		}
		var query struct {
			Kind     string `json:"kind"`
			Protocol int    `json:"protocol"`
		}
		if json.Unmarshal(packet[:count], &query) != nil || query.Kind != "massar-discover" || query.Protocol != protocol {
			continue
		}
		encoded, err := json.Marshal(g.description())
		if err != nil {
			continue
		}
		g.discovery.SetWriteDeadline(time.Now().Add(time.Second))
		if _, err = g.discovery.WriteToUDP(encoded, peer); err == nil {
			responses++
		}
	}
}
