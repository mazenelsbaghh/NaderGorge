package main

import (
	"bytes"
	"encoding/binary"
	"encoding/hex"
	"encoding/json"
	"errors"
	"net"
	"os"
	"path/filepath"
	"time"
)

const examIP = "10.77.0.1"

var serverIPv4 = net.ParseIP(examIP).To4()
var dhcpCookie = []byte{99, 130, 83, 99}

type lease struct {
	Address uint32 `json:"address"`
	Expires int64  `json:"expires"`
}
type leaseBook struct {
	Leases     map[string]lease `json:"leases"`
	Declined   map[uint32]int64 `json:"declined"`
	path       string
	apMAC      string
	capportURL string
}

type dhcpRequest struct {
	wire      []byte
	mac       string
	kind      byte
	requested uint32
	server    uint32
	current   uint32
}

func parseDHCP(packet []byte) (dhcpRequest, error) {
	var request dhcpRequest
	if len(packet) < 240 || len(packet) > 1500 || packet[0] != 1 || packet[1] != 1 || packet[2] != 6 || !bytes.Equal(packet[236:240], dhcpCookie) || !bytes.Equal(packet[24:28], []byte{0, 0, 0, 0}) {
		return request, errors.New("invalid direct DHCP request")
	}
	if packet[28]&1 != 0 || bytes.Equal(packet[28:34], make([]byte, 6)) {
		return request, errors.New("invalid hardware address")
	}
	request = dhcpRequest{wire: packet, mac: hex.EncodeToString(packet[28:34]), current: binary.BigEndian.Uint32(packet[12:16])}
	seen := map[byte]bool{}
	for offset := 240; offset < len(packet); {
		option := packet[offset]
		offset++
		if option == 255 {
			break
		}
		if option == 0 {
			continue
		}
		if offset >= len(packet) {
			return request, errors.New("truncated DHCP option")
		}
		size := int(packet[offset])
		offset++
		if offset+size > len(packet) || seen[option] {
			return request, errors.New("invalid DHCP option")
		}
		seen[option] = true
		value := packet[offset : offset+size]
		offset += size
		switch option {
		case 53:
			if size != 1 {
				return request, errors.New("invalid DHCP type")
			}
			request.kind = value[0]
		case 50, 54:
			if size != 4 {
				return request, errors.New("invalid DHCP address")
			}
			if option == 50 {
				request.requested = binary.BigEndian.Uint32(value)
			} else {
				request.server = binary.BigEndian.Uint32(value)
			}
		case 52:
			return request, errors.New("overloaded DHCP options are unsupported")
		}
	}
	if request.kind == 0 {
		return request, errors.New("missing DHCP type")
	}
	return request, nil
}

func loadLeases(path, apMAC string) (*leaseBook, error) {
	book := &leaseBook{Leases: map[string]lease{}, Declined: map[uint32]int64{}, path: path, apMAC: apMAC}
	content, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return book, nil
	}
	if err != nil {
		return nil, err
	}
	if len(content) > 512*1024 {
		return nil, errors.New("lease file too large")
	}
	if err = json.Unmarshal(content, book); err != nil {
		return nil, err
	}
	if book.Leases == nil || book.Declined == nil || len(book.Leases) > 1100 || len(book.Declined) > 1100 {
		return nil, errors.New("invalid lease file")
	}
	used := map[uint32]bool{}
	for mac, entry := range book.Leases {
		if len(mac) != 12 || used[entry.Address] || !validPoolAddress(entry.Address, mac, apMAC) {
			return nil, errors.New("invalid saved lease")
		}
		used[entry.Address] = true
	}
	return book, nil
}

func validPoolAddress(address uint32, mac, apMAC string) bool {
	return (address >= 0x0a4d0014 && address <= 0x0a4d03fa) || (address == 0x0a4d0002 && mac == apMAC && apMAC != "")
}
func (book *leaseBook) prune(now int64) {
	for mac, entry := range book.Leases {
		if entry.Expires <= now {
			delete(book.Leases, mac)
		}
	}
	for address, until := range book.Declined {
		if until <= now {
			delete(book.Declined, address)
		}
	}
}
func (book *leaseBook) save() error {
	content, err := json.Marshal(book)
	if err != nil {
		return err
	}
	return atomicWrite(book.path, content)
}
func atomicWrite(path string, content []byte) error {
	temporary, err := os.CreateTemp(filepath.Dir(path), ".massar-write-*")
	if err != nil {
		return err
	}
	name := temporary.Name()
	defer os.Remove(name)
	if err = temporary.Chmod(0600); err == nil {
		_, err = temporary.Write(content)
	}
	if err == nil {
		err = temporary.Sync()
	}
	closeErr := temporary.Close()
	if err == nil {
		err = closeErr
	}
	if err != nil {
		return err
	}
	return replaceFile(name, path)
}
func (book *leaseBook) available(address uint32, mac string) bool {
	if !validPoolAddress(address, mac, book.apMAC) || book.Declined[address] > 0 {
		return false
	}
	for owner, entry := range book.Leases {
		if entry.Address == address && owner != mac {
			return false
		}
	}
	return true
}
func (book *leaseBook) offer(request dhcpRequest, now int64) uint32 {
	if existing, ok := book.Leases[request.mac]; ok {
		return existing.Address
	}
	if request.mac == book.apMAC && book.available(0x0a4d0002, request.mac) {
		return 0x0a4d0002
	}
	if request.requested != 0 && book.available(request.requested, request.mac) {
		return request.requested
	}
	for address := uint32(0x0a4d0014); address <= 0x0a4d03fa; address++ {
		if book.available(address, request.mac) {
			return address
		}
	}
	return 0
}
func (book *leaseBook) reply(request dhcpRequest, now time.Time) ([]byte, error) {
	if request.server != 0 && request.server != 0x0a4d0001 {
		return nil, nil
	}
	book.prune(now.Unix())
	existing, exists := book.Leases[request.mac]
	switch request.kind {
	case 1:
		address := book.offer(request, now.Unix())
		if address == 0 {
			return nil, nil
		}
		if !exists {
			book.Leases[request.mac] = lease{address, now.Add(time.Minute).Unix()}
		}
		if err := book.save(); err != nil {
			return nil, err
		}
		return book.response(request, address, 2), nil
	case 3:
		address := request.requested
		if address == 0 {
			address = request.current
		}
		if !exists || address != existing.Address || !book.available(address, request.mac) {
			return book.response(request, 0, 6), nil
		}
		book.Leases[request.mac] = lease{address, now.Add(time.Hour).Unix()}
		if err := book.save(); err != nil {
			return nil, err
		}
		return book.response(request, address, 5), nil
	case 4:
		if exists && existing.Address == request.requested {
			delete(book.Leases, request.mac)
			book.Declined[existing.Address] = now.Add(10 * time.Minute).Unix()
			return nil, book.save()
		}
	case 7:
		if exists && existing.Address == request.current {
			delete(book.Leases, request.mac)
			return nil, book.save()
		}
	}
	return nil, nil
}
func dhcpResponse(request dhcpRequest, address uint32, kind byte) []byte {
	response := make([]byte, 240, 350)
	response[0] = 2
	response[1] = 1
	response[2] = 6
	copy(response[4:8], request.wire[4:8])
	copy(response[10:12], request.wire[10:12])
	copy(response[28:44], request.wire[28:44])
	copy(response[236:240], dhcpCookie)
	binary.BigEndian.PutUint32(response[16:20], address)
	copy(response[20:24], serverIPv4)
	response = append(response, 53, 1, kind, 54, 4, 10, 77, 0, 1)
	if kind != 6 {
		response = append(response, 1, 4, 255, 255, 252, 0, 3, 4, 10, 77, 0, 1, 6, 4, 10, 77, 0, 1, 51, 4, 0, 0, 14, 16, 58, 4, 0, 0, 7, 8, 59, 4, 0, 0, 12, 78)
	}
	response = append(response, 255)
	for len(response) < 300 {
		response = append(response, 0)
	}
	return response
}

func (book *leaseBook) response(request dhcpRequest, address uint32, kind byte) []byte {
	response := dhcpResponse(request, address, kind)
	if kind == 6 || book.capportURL == "" {
		return response
	}
	// Rebuild the option tail before END; padding is not an option value.
	offset := 240
	for offset < len(response) {
		code := response[offset]
		if code == 255 {
			break
		}
		if code == 0 {
			offset++
			continue
		}
		offset += 2 + int(response[offset+1])
	}
	response = response[:offset]
	response = append(response, 114, byte(len(book.capportURL)))
	response = append(response, []byte(book.capportURL)...)
	response = append(response, 255)
	for len(response) < 300 {
		response = append(response, 0)
	}
	return response
}
