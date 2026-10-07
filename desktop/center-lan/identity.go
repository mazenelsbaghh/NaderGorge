package main

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/sha256"
	"crypto/tls"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/hex"
	"encoding/json"
	"encoding/pem"
	"errors"
	"math/big"
	"net"
	"os"
	"path/filepath"
	"time"
)

type savedIdentity struct {
	HostID      string `json:"hostId"`
	Certificate string `json:"certificate"`
	PrivateKey  string `json:"privateKey"`
}

type Identity struct {
	HostID      string
	Certificate tls.Certificate
	Fingerprint string
}

func loadIdentity(directory string) (Identity, error) {
	if err := os.MkdirAll(directory, 0700); err != nil {
		return Identity{}, err
	}
	path := filepath.Join(directory, "identity.json")
	contents, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		saved, createErr := newSavedIdentity()
		if createErr != nil {
			return Identity{}, createErr
		}
		if err = persistJSON(path, saved); err != nil {
			return Identity{}, err
		}
		contents, err = json.Marshal(saved)
	}
	if err != nil {
		return Identity{}, err
	}
	var saved savedIdentity
	if err = json.Unmarshal(contents, &saved); err != nil {
		return Identity{}, err
	}
	certificate, err := tls.X509KeyPair([]byte(saved.Certificate), []byte(saved.PrivateKey))
	if err != nil {
		return Identity{}, err
	}
	parsed, err := x509.ParseCertificate(certificate.Certificate[0])
	if err != nil || !time.Now().Before(parsed.NotAfter) || saved.HostID == "" {
		return Identity{}, errors.New("invalid or expired host certificate")
	}
	fingerprint := sha256.Sum256(certificate.Certificate[0])
	return Identity{saved.HostID, certificate, hex.EncodeToString(fingerprint[:])}, nil
}

func newSavedIdentity() (savedIdentity, error) {
	// Flutter uses BoringSSL; P-256 is supported by its TLS signature algorithms.
	privateKey, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		return savedIdentity{}, err
	}
	hostBytes := make([]byte, 16)
	if _, err = rand.Read(hostBytes); err != nil {
		return savedIdentity{}, err
	}
	hostID := hex.EncodeToString(hostBytes)
	serial, err := rand.Int(rand.Reader, new(big.Int).Lsh(big.NewInt(1), 128))
	if err != nil {
		return savedIdentity{}, err
	}
	template := x509.Certificate{
		SerialNumber: serial, Subject: pkix.Name{CommonName: "Massar LAN " + hostID},
		NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().AddDate(10, 0, 0),
		KeyUsage: x509.KeyUsageDigitalSignature, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth},
		DNSNames: []string{"localhost"}, IPAddresses: []net.IP{net.ParseIP("127.0.0.1"), net.ParseIP("::1")},
	}
	encoded, err := x509.CreateCertificate(rand.Reader, &template, &template, &privateKey.PublicKey, privateKey)
	if err != nil {
		return savedIdentity{}, err
	}
	key, err := x509.MarshalPKCS8PrivateKey(privateKey)
	if err != nil {
		return savedIdentity{}, err
	}
	return savedIdentity{hostID, string(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: encoded})), string(pem.EncodeToMemory(&pem.Block{Type: "PRIVATE KEY", Bytes: key}))}, nil
}

func persistJSON(path string, record any) error {
	encoded, err := json.Marshal(record)
	if err != nil {
		return err
	}
	staging, err := os.CreateTemp(filepath.Dir(path), ".massar-*.tmp")
	if err != nil {
		return err
	}
	defer os.Remove(staging.Name())
	if err = staging.Chmod(0600); err == nil {
		_, err = staging.Write(encoded)
	}
	if err == nil {
		err = staging.Sync()
	}
	closeErr := staging.Close()
	if err != nil {
		return err
	}
	if closeErr != nil {
		return closeErr
	}
	return os.Rename(staging.Name(), path)
}
