package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"net"
	"os"
	"path/filepath"
	"testing"
	"time"
)

func clientPacket(mac []byte, kind byte, address uint32) []byte {
	packet := make([]byte, 240)
	packet[0] = 1
	packet[1] = 1
	packet[2] = 6
	packet[7] = 42
	packet[10] = 0x80
	copy(packet[28:34], mac)
	copy(packet[236:], dhcpCookie)
	packet = append(packet, 53, 1, kind)
	if address != 0 {
		packet = append(packet, 50, 4, byte(address>>24), byte(address>>16), byte(address>>8), byte(address))
	}
	return append(packet, 255)
}
func parseTestRequest(t *testing.T, packet []byte) dhcpRequest {
	t.Helper()
	request, err := parseDHCP(packet)
	if err != nil {
		t.Fatal(err)
	}
	return request
}
func TestDHCPReservationsSurviveRestartAndDoNotOverlap(t *testing.T) {
	path := filepath.Join(t.TempDir(), "leases.json")
	book, err := loadLeases(path, "20e15dd36f52")
	if err != nil {
		t.Fatal(err)
	}
	now := time.Date(2026, 10, 12, 9, 0, 0, 0, time.UTC)
	firstMAC := []byte{2, 1, 2, 3, 4, 5}
	first := parseTestRequest(t, clientPacket(firstMAC, 1, 0))
	offer, err := book.reply(first, now)
	if err != nil {
		t.Fatal(err)
	}
	address := binary.BigEndian.Uint32(offer[16:20])
	if address != 0x0a4d0014 {
		t.Fatalf("first client address=%x", address)
	}
	request := parseTestRequest(t, clientPacket(firstMAC, 3, address))
	ack, err := book.reply(request, now)
	if err != nil {
		t.Fatal(err)
	}
	if ack[242] != 5 || !bytes.Equal(ack[4:8], request.wire[4:8]) {
		t.Fatal("lease was not acknowledged with request transaction")
	}
	restarted, err := loadLeases(path, "20e15dd36f52")
	if err != nil {
		t.Fatal(err)
	}
	second := parseTestRequest(t, clientPacket([]byte{2, 1, 2, 3, 4, 6}, 1, address))
	secondOffer, err := restarted.reply(second, now.Add(time.Second))
	if err != nil {
		t.Fatal(err)
	}
	if binary.BigEndian.Uint32(secondOffer[16:20]) == address {
		t.Fatal("address assigned twice after restart")
	}
	renewed, err := restarted.reply(request, now.Add(30*time.Minute))
	if err != nil || renewed[242] != 5 {
		t.Fatal("existing lease renewal failed", err)
	}
	accessPoint := parseTestRequest(t, clientPacket([]byte{0x20, 0xe1, 0x5d, 0xd3, 0x6f, 0x52}, 1, 0))
	reserved, err := restarted.reply(accessPoint, now)
	if err != nil {
		t.Fatal(err)
	}
	if binary.BigEndian.Uint32(reserved[16:20]) != 0x0a4d0002 {
		t.Fatal("AP reservation was lost")
	}
}
func TestDHCPScopeAndPersistenceFailuresNeverGrantLease(t *testing.T) {
	for _, scenario := range []struct {
		name   string
		mutate func([]byte) []byte
	}{
		{"relay", func(packet []byte) []byte { packet[24] = 10; return packet }},
		{"multicast hardware", func(packet []byte) []byte { packet[28] = 1; return packet }},
		{"truncated option", func(packet []byte) []byte { return append(packet[:len(packet)-1], 50, 4, 10) }},
		{"duplicate type", func(packet []byte) []byte { return append(packet[:len(packet)-1], 53, 1, 3, 255) }},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			if _, err := parseDHCP(scenario.mutate(clientPacket([]byte{2, 3, 4, 5, 6, 7}, 1, 0))); err == nil {
				t.Fatal("unsafe DHCP request accepted")
			}
		})
	}
	path := filepath.Join(t.TempDir(), "leases.json")
	book, _ := loadLeases(path, "")
	foreignPacket := clientPacket([]byte{2, 3, 4, 5, 6, 7}, 3, 0x0a4d0014)
	foreign := parseTestRequest(t, append(foreignPacket[:len(foreignPacket)-1], 54, 4, 192, 168, 1, 1, 255))
	if answer, err := book.reply(foreign, time.Now()); err != nil || answer != nil {
		t.Fatal("answered foreign DHCP server")
	}
	book.path = filepath.Join(path, "missing", "leases.json")
	if answer, err := book.reply(parseTestRequest(t, clientPacket([]byte{2, 3, 4, 5, 6, 7}, 1, 0)), time.Now()); err == nil || answer != nil {
		t.Fatal("advertised a lease that was not persisted")
	}
}
func TestDHCPDeclineQuarantineExpiryAndCAPPORT(t *testing.T) {
	now := time.Now()
	book, _ := loadLeases(filepath.Join(t.TempDir(), "leases.json"), "")
	book.capportURL = "https://exam.example.test/capport"
	firstMAC := []byte{2, 1, 2, 3, 4, 5}
	offer, err := book.reply(parseTestRequest(t, clientPacket(firstMAC, 1, 0)), now)
	if err != nil {
		t.Fatal(err)
	}
	address := binary.BigEndian.Uint32(offer[16:20])
	if !bytes.Contains(offer, append([]byte{114, byte(len(book.capportURL))}, []byte(book.capportURL)...)) {
		t.Fatal("CAPPORT discovery missing")
	}
	if _, err = book.reply(parseTestRequest(t, clientPacket(firstMAC, 4, address)), now); err != nil {
		t.Fatal(err)
	}
	secondMAC := []byte{2, 1, 2, 3, 4, 6}
	offer, err = book.reply(parseTestRequest(t, clientPacket(secondMAC, 1, address)), now.Add(time.Minute))
	if err != nil {
		t.Fatal(err)
	}
	if binary.BigEndian.Uint32(offer[16:20]) == address {
		t.Fatal("declined address was reused during quarantine")
	}
	offer, err = book.reply(parseTestRequest(t, clientPacket(firstMAC, 1, address)), now.Add(11*time.Minute))
	if err != nil {
		t.Fatal(err)
	}
	if binary.BigEndian.Uint32(offer[16:20]) != address {
		t.Fatal("expired quarantine did not release address")
	}
}
func TestDNSOfflineResponseAndRealUDP(t *testing.T) {
	query := []byte{1, 2, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 4, 't', 'e', 's', 't', 0, 0, 1, 0, 1}
	for _, kind := range []uint16{1, 28, 65} {
		t.Run(fmt.Sprintf("type-%d", kind), func(t *testing.T) {
			request := append([]byte(nil), query...)
			binary.BigEndian.PutUint16(request[len(request)-4:], kind)
			response, err := dnsResponse(request)
			if err != nil {
				t.Fatal(err)
			}
			if kind == 1 && (response[7] != 1 || !bytes.Equal(response[len(response)-4:], serverIPv4)) {
				t.Fatal("DNS did not resolve to exam host")
			}
			if kind != 1 && (response[7] != 0 || response[3]&15 != 0) {
				t.Fatal("non-A query should return NODATA without upstream")
			}
		})
	}
	for _, bad := range [][]byte{query[:17], append(append([]byte(nil), query[:12]...), 0xc0, 0x0c, 0, 1, 0, 1)} {
		if _, err := dnsResponse(bad); err == nil {
			t.Fatal("malformed DNS query accepted")
		}
	}
	// Exercise packet framing over a real loopback socket while retaining the production response parser.
	server, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
	if err != nil {
		t.Fatal(err)
	}
	defer server.Close()
	completed := make(chan error, 1)
	go func() {
		packet := make([]byte, 512)
		n, peer, readErr := server.ReadFromUDP(packet)
		if readErr == nil {
			var answer []byte
			answer, readErr = dnsResponse(packet[:n])
			if readErr == nil {
				_, readErr = server.WriteToUDP(answer, peer)
			}
		}
		completed <- readErr
	}()
	client, err := net.DialUDP("udp4", nil, server.LocalAddr().(*net.UDPAddr))
	if err != nil {
		t.Fatal(err)
	}
	defer client.Close()
	client.SetDeadline(time.Now().Add(time.Second))
	if _, err = client.Write(query); err != nil {
		t.Fatal(err)
	}
	answer := make([]byte, 512)
	n, err := client.Read(answer)
	if err != nil {
		select {
		case workerErr := <-completed:
			t.Fatalf("client %v, server %v", err, workerErr)
		default:
			t.Fatal(err)
		}
	}
	if !bytes.Equal(answer[n-4:n], serverIPv4) {
		t.Fatal("wrong DNS address over UDP")
	}
	if err = <-completed; err != nil {
		t.Fatal(err)
	}
}
func TestCorruptLeaseFileCannotReassignActiveReservations(t *testing.T) {
	path := filepath.Join(t.TempDir(), "leases.json")
	os.WriteFile(path, []byte(`{"leases":{"020102030405":{"address":172818452,"expires":9999999999},"020102030406":{"address":172818452,"expires":9999999999}},"declined":{}}`), 0600)
	if _, err := loadLeases(path, ""); err == nil {
		t.Fatal("duplicate saved reservations accepted")
	}
}
