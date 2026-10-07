package main

import (
	"crypto/rand"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"math/big"
	"os"
	"path/filepath"
	"regexp"
	"sync"
	"time"
)

const pairingLifetime = 10 * time.Minute

var deviceIDPattern = regexp.MustCompile(`^[A-Za-z0-9._-]{1,100}$`)

type Device struct {
	DeviceID string    `json:"deviceId"`
	Name     string    `json:"name"`
	PairedAt time.Time `json:"pairedAt"`
	Revoked  bool      `json:"revoked"`
}

type savedDevice struct {
	Device
	TokenHash string `json:"tokenHash"`
}

type Pairing struct {
	mu            sync.Mutex
	path          string
	devices       []savedDevice
	code          string
	expires       time.Time
	attempts      map[string]int
	totalAttempts int
}

type PairRequest struct {
	Code     string `json:"code"`
	DeviceID string `json:"deviceId"`
	Name     string `json:"name"`
}
type PairingCode struct {
	Code      string    `json:"pairingCode"`
	ExpiresAt time.Time `json:"expiresAt"`
}

var errPairRejected = errors.New("pairing rejected")
var errPairRate = errors.New("pairing rate limited")

func loadPairing(directory string) (*Pairing, error) {
	pairing := &Pairing{path: filepath.Join(directory, "devices.json")}
	contents, err := os.ReadFile(pairing.path)
	if err == nil {
		if err = json.Unmarshal(contents, &pairing.devices); err != nil {
			return nil, err
		}
		seen := map[string]bool{}
		for _, device := range pairing.devices {
			if !deviceIDPattern.MatchString(device.DeviceID) || !validName(device.Name) || len(device.TokenHash) != 64 || seen[device.DeviceID] {
				return nil, errors.New("invalid device registry")
			}
			seen[device.DeviceID] = true
		}
	} else if !errors.Is(err, os.ErrNotExist) {
		return nil, err
	}
	if _, err = pairing.rotate(); err != nil {
		return nil, err
	}
	return pairing, nil
}

func (p *Pairing) rotate() (PairingCode, error) {
	random, err := rand.Int(rand.Reader, big.NewInt(1000000))
	if err != nil {
		return PairingCode{}, err
	}
	p.mu.Lock()
	defer p.mu.Unlock()
	p.code = fmt.Sprintf("%06d", random.Int64())
	p.expires = time.Now().UTC().Add(pairingLifetime)
	p.attempts = map[string]int{}
	p.totalAttempts = 0
	return PairingCode{p.code, p.expires}, nil
}

func (p *Pairing) currentCode() PairingCode {
	p.mu.Lock()
	defer p.mu.Unlock()
	return PairingCode{p.code, p.expires}
}

func (p *Pairing) pair(request PairRequest, remoteIP string) (string, error) {
	p.mu.Lock()
	defer p.mu.Unlock()
	if p.attempts[remoteIP] >= 5 || p.totalAttempts >= 30 {
		return "", errPairRate
	}
	if !time.Now().Before(p.expires) || len(request.Code) != 6 || subtle.ConstantTimeCompare([]byte(request.Code), []byte(p.code)) != 1 {
		p.attempts[remoteIP]++
		p.totalAttempts++
		return "", errPairRejected
	}
	if !deviceIDPattern.MatchString(request.DeviceID) || !validName(request.Name) {
		return "", errPairRejected
	}
	tokenBytes := make([]byte, 32)
	if _, err := rand.Read(tokenBytes); err != nil {
		return "", err
	}
	token := base64.RawURLEncoding.EncodeToString(tokenBytes)
	registered := savedDevice{Device: Device{request.DeviceID, request.Name, time.Now().UTC(), false}, TokenHash: hashToken(token)}
	next := append([]savedDevice(nil), p.devices...)
	replaced := false
	for i := range next {
		if next[i].DeviceID == request.DeviceID {
			next[i] = registered
			replaced = true
		}
	}
	if !replaced {
		if len(next) >= 100 {
			return "", errPairRejected
		}
		next = append(next, registered)
	}
	if err := persistJSON(p.path, next); err != nil {
		return "", err
	}
	p.devices = next
	return token, nil
}

func hashToken(token string) string {
	digest := sha256.Sum256([]byte(token))
	return hex.EncodeToString(digest[:])
}

func (p *Pairing) authenticatedDevice(token string) (Device, bool) {
	if len(token) != 43 {
		return Device{}, false
	}
	wanted := hashToken(token)
	p.mu.Lock()
	defer p.mu.Unlock()
	for _, device := range p.devices {
		if !device.Revoked && subtle.ConstantTimeCompare([]byte(device.TokenHash), []byte(wanted)) == 1 {
			return device.Device, true
		}
	}
	return Device{}, false
}

func (p *Pairing) list() []Device {
	p.mu.Lock()
	defer p.mu.Unlock()
	devices := make([]Device, 0, len(p.devices))
	for _, device := range p.devices {
		devices = append(devices, device.Device)
	}
	return devices
}

func (p *Pairing) revoke(deviceID string) error {
	p.mu.Lock()
	defer p.mu.Unlock()
	next := append([]savedDevice(nil), p.devices...)
	found := false
	for i := range next {
		if next[i].DeviceID == deviceID {
			next[i].Revoked = true
			found = true
		}
	}
	if !found {
		return os.ErrNotExist
	}
	if err := persistJSON(p.path, next); err != nil {
		return err
	}
	p.devices = next
	return nil
}
