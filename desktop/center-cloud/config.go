package main

import (
	"encoding/hex"
	"encoding/json"
	"errors"
	"net"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
)

var identifierPattern = regexp.MustCompile(`^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$`)
var uuidPattern = regexp.MustCompile(`^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$`)
var hashPattern = regexp.MustCompile(`^[0-9a-f]{64}$`)
var buildPattern = regexp.MustCompile(`^[0-9a-f]{16}$`)
var versionPattern = regexp.MustCompile(`^[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6}(?:-[0-9A-Za-z][0-9A-Za-z.-]{0,31})?\+[0-9]{1,10}$`)
var releaseFilePattern = regexp.MustCompile(`^[A-Za-z0-9][A-Za-z0-9._-]{0,199}\.zip$`)

const maximumDiagnosticsBytes = 6 * 1024 * 1024

// Credentials are hashes supplied by the operator, never part of a release or log.
type config struct {
	listen         string
	storageDir     string
	updatesDir     string
	devices        map[string][32]byte
	adminHash      [32]byte
	maxUploadBytes int64
}

func readConfig() (config, error) {
	invalid := errors.New("invalid service configuration")
	c := config{listen: os.Getenv("MASSAR_SUPPORT_LISTEN"), maxUploadBytes: 128 * 1024 * 1024}
	if c.listen == "" {
		c.listen = "127.0.0.1:43880"
	}
	host, port, err := net.SplitHostPort(c.listen)
	if err != nil {
		return c, invalid
	}
	ip := net.ParseIP(host)
	portNumber, err := strconv.Atoi(port)
	// Only a local TLS reverse proxy may reach the unencrypted upstream.
	if ip == nil || !ip.IsLoopback() || err != nil || portNumber < 1 || portNumber > 65535 {
		return c, invalid
	}
	if value := os.Getenv("MASSAR_SUPPORT_MAX_UPLOAD_BYTES"); value != "" {
		c.maxUploadBytes, err = strconv.ParseInt(value, 10, 64)
		if err != nil || c.maxUploadBytes < 1024 || c.maxUploadBytes > 1024*1024*1024 {
			return c, invalid
		}
	}
	c.storageDir = os.Getenv("MASSAR_SUPPORT_STORAGE_DIR")
	c.updatesDir = os.Getenv("MASSAR_SUPPORT_UPDATES_DIR")
	if !filepath.IsAbs(c.storageDir) || !filepath.IsAbs(c.updatesDir) {
		return c, invalid
	}
	c.storageDir = filepath.Clean(c.storageDir)
	c.updatesDir = filepath.Clean(c.updatesDir)
	if inside(c.storageDir, c.updatesDir) || inside(c.updatesDir, c.storageDir) {
		return c, invalid
	}
	var deviceHashes map[string]string
	if err := json.Unmarshal([]byte(os.Getenv("MASSAR_SUPPORT_DEVICES_JSON")), &deviceHashes); err != nil || len(deviceHashes) == 0 || len(deviceHashes) > 1000 {
		return c, invalid
	}
	c.devices = make(map[string][32]byte, len(deviceHashes))
	seen := make(map[[32]byte]bool)
	admin, ok := parseHash(os.Getenv("MASSAR_SUPPORT_ADMIN_TOKEN_SHA256"))
	if !ok {
		return c, invalid
	}
	c.adminHash = admin
	for centerID, value := range deviceHashes {
		hash, ok := parseHash(value)
		if !identifierPattern.MatchString(centerID) || !ok || hash == admin || seen[hash] {
			return c, invalid
		}
		seen[hash] = true
		c.devices[centerID] = hash
	}
	return c, nil
}

func parseHash(value string) ([32]byte, bool) {
	var result [32]byte
	if !hashPattern.MatchString(value) {
		return result, false
	}
	decoded, err := hex.DecodeString(value)
	if err != nil {
		return result, false
	}
	copy(result[:], decoded)
	if result == [32]byte{} {
		return result, false
	}
	return result, true
}

func inside(parent, child string) bool {
	relative, err := filepath.Rel(parent, child)
	return err == nil && relative != ".." && !strings.HasPrefix(relative, ".."+string(filepath.Separator))
}
