package main

import (
	"encoding/binary"
	"errors"
	"io"
	"net"
	"time"
)

func dnsResponse(query []byte) ([]byte, error) {
	if len(query) < 17 || len(query) > 4096 || query[2]&0xf8 != 0 || binary.BigEndian.Uint16(query[4:6]) != 1 {
		return nil, errors.New("invalid DNS question")
	}
	end := 12
	for labels := 0; ; labels++ {
		if end >= len(query) || labels > 127 {
			return nil, errors.New("truncated DNS name")
		}
		size := int(query[end])
		end++
		if size == 0 {
			break
		}
		if size > 63 || end+size > len(query) {
			return nil, errors.New("invalid DNS name")
		}
		end += size
	}
	if end > 267 || end+4 > len(query) {
		return nil, errors.New("invalid DNS question length")
	}
	kind := binary.BigEndian.Uint16(query[end : end+2])
	class := binary.BigEndian.Uint16(query[end+2 : end+4])
	end += 4
	response := append([]byte(nil), query[:end]...)
	response[2] = 0x84 | (query[2] & 1)
	response[3] = 0
	for index := 6; index < 12; index++ {
		response[index] = 0
	}
	if class == 1 && kind == 1 {
		response[7] = 1
		response = append(response, 0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 30, 0, 4, 10, 77, 0, 1)
	}
	return response, nil
}
func serveDNSUDP(socket *net.UDPConn) error {
	packet := make([]byte, 4096)
	for {
		n, peer, err := socket.ReadFromUDP(packet)
		if err != nil {
			return err
		}
		if !examClient(peer.IP) {
			continue
		}
		answer, err := dnsResponse(packet[:n])
		if err != nil {
			continue
		}
		if _, err = socket.WriteToUDP(answer, peer); err != nil {
			return err
		}
	}
}
func examClient(ip net.IP) bool {
	v := ip.To4()
	return v != nil && v[0] == 10 && v[1] == 77 && v[2] < 4
}
func serveDNSTCP(listener net.Listener) error {
	slots := make(chan struct{}, 32)
	for {
		connection, err := listener.Accept()
		if err != nil {
			return err
		}
		address, ok := connection.RemoteAddr().(*net.TCPAddr)
		if !ok || !examClient(address.IP) {
			connection.Close()
			continue
		}
		select {
		case slots <- struct{}{}:
			go func() { defer func() { <-slots }(); dnsConnection(connection) }()
		default:
			connection.Close()
		}
	}
}
func dnsConnection(connection net.Conn) {
	defer connection.Close()
	connection.SetDeadline(time.Now().Add(5 * time.Second))
	size := make([]byte, 2)
	if _, err := io.ReadFull(connection, size); err != nil {
		return
	}
	length := binary.BigEndian.Uint16(size)
	if length < 17 || length > 4096 {
		return
	}
	packet := make([]byte, length)
	if _, err := io.ReadFull(connection, packet); err != nil {
		return
	}
	answer, err := dnsResponse(packet)
	if err != nil {
		return
	}
	binary.BigEndian.PutUint16(size, uint16(len(answer)))
	connection.Write(append(size, answer...))
}
