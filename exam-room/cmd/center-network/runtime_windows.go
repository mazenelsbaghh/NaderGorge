//go:build windows

package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"net"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"syscall"
	"time"
)

type networkConfig struct {
	SchemaVersion int    `json:"schemaVersion"`
	AdapterID     string `json:"adapterId"`
	AdapterIndex  int    `json:"adapterIndex"`
	APMAC         string `json:"apMac"`
	PortalMode    string `json:"portalMode"`
	Hostname      string `json:"hostname"`
}

func runtimeConfig(root string) (networkConfig, error) {
	var configuration networkConfig
	content, err := os.ReadFile(filepath.Join(root, "network.json"))
	if err != nil {
		return configuration, err
	}
	if err = json.Unmarshal(bytes.TrimPrefix(content, []byte{0xef, 0xbb, 0xbf}), &configuration); err != nil {
		return configuration, err
	}
	if configuration.SchemaVersion != 1 || !adapterGUID.MatchString(configuration.AdapterID) || configuration.AdapterIndex < 1 {
		return configuration, errors.New("invalid network configuration")
	}
	return configuration, nil
}
func examAdapter(configuration networkConfig) error {
	// Resolve the saved GUID every startup; Windows can reuse interface indices.
	response, err := invokeScript("list-adapters", "")
	if err != nil {
		return err
	}
	adapters, ok := response["adapters"].([]any)
	if !ok {
		return errors.New("adapter list unavailable")
	}
	for _, candidate := range adapters {
		adapter, ok := candidate.(map[string]any)
		if !ok {
			continue
		}
		if !strings.EqualFold(fmt.Sprint(adapter["id"]), configuration.AdapterID) {
			continue
		}
		if index, ok := adapter["index"].(float64); !ok || int(index) != configuration.AdapterIndex {
			return errors.New("selected adapter index changed; run setup again")
		}
		if addresses, ok := adapter["ipv4"].([]any); ok {
			for _, address := range addresses {
				if address == examIP {
					return nil
				}
			}
		}
	}
	return errors.New("selected exam adapter or static address is unavailable")
}
func runNetwork() error {
	root := filepath.Join(os.Getenv("ProgramData"), "Massar", "ExamNetwork")
	configuration, err := runtimeConfig(root)
	if err != nil {
		return err
	}
	if err = examAdapter(configuration); err != nil {
		return err
	}
	book, err := loadLeases(filepath.Join(root, "leases.json"), configuration.APMAC)
	if err != nil {
		return err
	}
	if configuration.PortalMode == "https" {
		book.capportURL = "https://" + configuration.Hostname + "/capport"
	}
	dhcp, err := net.ListenUDP("udp4", &net.UDPAddr{Port: 67})
	if err != nil {
		return err
	}
	defer dhcp.Close()
	if err = enablePacketInfo(dhcp, configuration.AdapterIndex); err != nil {
		return err
	}
	dnsUDP, err := net.ListenUDP("udp4", &net.UDPAddr{IP: serverIPv4, Port: 53})
	if err != nil {
		return err
	}
	defer dnsUDP.Close()
	dnsTCP, err := net.Listen("tcp4", examIP+":53")
	if err != nil {
		return err
	}
	defer dnsTCP.Close()
	portalArgs := []string{"--listen", examIP + ":80", "--diagnostic-log", filepath.Join(root, "portal-events.jsonl")}
	if configuration.PortalMode == "https" {
		portalArgs = append(portalArgs, "--https-listen", examIP+":443", "--capport-hostname", configuration.Hostname, "--capport-cert", filepath.Join(root, "capport", "fullchain.pem"), "--capport-key", filepath.Join(root, "capport", "privkey.pem"))
	}
	portal := exec.Command(filepath.Join(root, "massar-exam-portal-proxy.exe"), portalArgs...)
	portal.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	portalLog, err := os.OpenFile(filepath.Join(root, "portal-runtime.log"), os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0600)
	if err != nil {
		return err
	}
	defer portalLog.Close()
	portal.Stdout = portalLog
	portal.Stderr = portalLog
	if err = portal.Start(); err != nil {
		return err
	}
	defer portal.Process.Kill()
	failures := make(chan error, 4)
	go func() { failures <- portal.Wait() }()
	go func() { failures <- serveDNSUDP(dnsUDP) }()
	go func() { failures <- serveDNSTCP(dnsTCP) }()
	go func() { failures <- serveDHCP(dhcp, book, configuration.AdapterIndex) }()
	ticker := time.NewTicker(5 * time.Second)
	defer ticker.Stop()
	heartbeat := func() error {
		content, _ := json.Marshal(output{"at": time.Now().Unix(), "adapterId": configuration.AdapterID, "dhcp": true})
		return atomicWrite(filepath.Join(root, "heartbeat.json"), content)
	}
	if err = heartbeat(); err != nil {
		return err
	}
	for {
		select {
		case err := <-failures:
			if err == nil {
				return errors.New("network worker exited")
			}
			return err
		case <-ticker.C:
			if err = heartbeat(); err != nil {
				return err
			}
		}
	}
}
func serveDHCP(socket *net.UDPConn, book *leaseBook, index int) error {
	packet := make([]byte, 1500)
	control := make([]byte, 128)
	for {
		n, controlSize, _, peer, err := socket.ReadMsgUDP(packet, control)
		if err != nil {
			return err
		}
		if peer.Port != 68 || packetInterface(control[:controlSize]) != index {
			continue
		}
		request, err := parseDHCP(packet[:n])
		if err != nil {
			continue
		}
		answer, err := book.reply(request, time.Now())
		if err != nil {
			return errors.New("could not persist DHCP reservation")
		}
		if len(answer) == 0 {
			continue
		}
		destination := &net.UDPAddr{IP: net.IPv4bcast, Port: 68}
		if request.current != 0 && request.wire[10]&0x80 == 0 && answer[242] != 6 {
			destination.IP = net.IPv4(byte(request.current>>24), byte(request.current>>16), byte(request.current>>8), byte(request.current))
		}
		if _, err = socket.WriteToUDP(answer, destination); err != nil {
			return err
		}
	}
}
func hotspot(request input) output {
	executable, err := os.Executable()
	if err != nil {
		return output{"ok": false, "message": "تعذر العثور على أداة شبكة الهوست."}
	}
	helper := filepath.Join(filepath.Dir(executable), "hotspot", "Massar.Hotspot.exe")
	if _, err = os.Stat(helper); err != nil {
		return output{"ok": false, "supported": false, "active": false, "message": "مكوّن شبكة الهوست غير موجود في الحزمة؛ ثبّت حزمة ويندوز الكاملة."}
	}
	command := exec.Command(helper)
	command.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	encoded, _ := json.Marshal(map[string]string{"action": strings.TrimPrefix(request.Action, "hotspot-"), "ssid": request.SSID, "password": request.Password})
	command.Stdin = bytes.NewReader(encoded)
	content, err := command.Output()
	if err != nil {
		return output{"ok": false, "supported": true, "active": false, "message": "تعذر تشغيل شبكة الهوست؛ راجع دعم Wi-Fi Direct ومحول الواي فاي."}
	}
	var response output
	if json.Unmarshal(content, &response) != nil {
		return output{"ok": false, "message": "تعذر قراءة حالة شبكة الهوست."}
	}
	return response
}
