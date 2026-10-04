package main

import (
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"
)

type appMetadata struct {
	Version string `json:"version"`
	Build   string `json:"build"`
	Role    string `json:"role"`
	OS      string `json:"os"`
}

type uploadEnvelope struct {
	Format      string          `json:"format"`
	Kind        string          `json:"kind,omitempty"`
	UploadID    string          `json:"uploadId"`
	CenterID    string          `json:"centerId"`
	CreatedAt   string          `json:"createdAt"`
	App         appMetadata     `json:"app"`
	Data        json.RawMessage `json:"data"`
	Diagnostics string          `json:"diagnostics"`
}

type uploadReceipt struct {
	ReceiptID    string      `json:"receiptId"`
	UploadID     string      `json:"uploadId"`
	CenterID     string      `json:"centerId"`
	SHA256       string      `json:"sha256"`
	BundleSHA256 string      `json:"bundleSha256"`
	ReceivedAt   string      `json:"receivedAt"`
	CreatedAt    string      `json:"createdAt"`
	Size         int64       `json:"size"`
	App          appMetadata `json:"app"`
}

var errConflict = errors.New("upload identifier conflict")
var errTooLarge = errors.New("upload too large")

func validMetadata(app appMetadata) bool {
	return (app.Version == "development" || versionPattern.MatchString(app.Version)) &&
		(app.Build == "development" || buildPattern.MatchString(app.Build)) &&
		(app.Role == "host" || app.Role == "client") &&
		(app.OS == "windows" || app.OS == "macos" || app.OS == "linux" || app.OS == "android" || app.OS == "ios" || app.OS == "fuchsia")
}

func validUTC(value string) bool {
	if len(value) > 40 || len(value) < 20 || value[len(value)-1] != 'Z' {
		return false
	}
	_, err := time.Parse(time.RFC3339Nano, value)
	return err == nil
}

func newUUID() (string, error) {
	var value [16]byte
	if _, err := rand.Read(value[:]); err != nil {
		return "", err
	}
	value[6] = (value[6] & 0x0f) | 0x40
	value[8] = (value[8] & 0x3f) | 0x80
	return fmt.Sprintf("%x-%x-%x-%x-%x", value[0:4], value[4:6], value[6:8], value[8:10], value[10:16]), nil
}

func sumHex(data []byte) string {
	sum := sha256.Sum256(data)
	return hex.EncodeToString(sum[:])
}

func syncDirectory(directory string) error {
	file, err := os.Open(directory)
	if err != nil {
		return err
	}
	defer file.Close()
	return file.Sync()
}

func writePrivateFile(name string, value []byte) error {
	file, err := os.OpenFile(name, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0600)
	if err != nil {
		return err
	}
	if _, err := file.Write(value); err != nil {
		file.Close()
		return err
	}
	if err := file.Sync(); err != nil {
		file.Close()
		return err
	}
	return file.Close()
}

// Publish two fully synced files as one non-empty directory. A failed or crashed
// writer cannot expose a receipt before its corresponding complete bundle.
func (s *supportService) saveUpload(envelope uploadEnvelope, requestHash string) (uploadReceipt, bool, error) {
	if receipt, err := s.readReceipt(envelope.UploadID); err == nil {
		if receipt.CenterID != envelope.CenterID || receipt.SHA256 != requestHash {
			return receipt, false, errConflict
		}
		if err := s.verifyBundle(receipt); err != nil {
			return receipt, false, err
		}
		if err := syncDirectory(s.config.storageDir); err != nil {
			return receipt, false, err
		}
		return receipt, false, nil
	} else if !os.IsNotExist(err) {
		return uploadReceipt{}, false, err
	}
	bundle, err := json.Marshal(envelope)
	if err != nil {
		return uploadReceipt{}, false, err
	}
	if int64(len(bundle)) > s.config.maxUploadBytes {
		return uploadReceipt{}, false, errTooLarge
	}
	receiptID, err := newUUID()
	if err != nil {
		return uploadReceipt{}, false, err
	}
	receipt := uploadReceipt{
		ReceiptID: receiptID, UploadID: envelope.UploadID, CenterID: envelope.CenterID,
		SHA256: requestHash, BundleSHA256: sumHex(bundle), Size: int64(len(bundle)),
		ReceivedAt: time.Now().UTC().Format(time.RFC3339Nano), CreatedAt: envelope.CreatedAt, App: envelope.App,
	}
	metadata, err := json.Marshal(receipt)
	if err != nil {
		return uploadReceipt{}, false, err
	}
	stage, err := os.MkdirTemp(filepath.Join(s.config.storageDir, ".staging"), "upload-")
	if err != nil {
		return uploadReceipt{}, false, err
	}
	defer os.RemoveAll(stage)
	if err := writePrivateFile(filepath.Join(stage, "bundle.json"), bundle); err != nil {
		return uploadReceipt{}, false, err
	}
	if err := writePrivateFile(filepath.Join(stage, "receipt.json"), metadata); err != nil {
		return uploadReceipt{}, false, err
	}
	if err := syncDirectory(stage); err != nil {
		return uploadReceipt{}, false, err
	}
	destination := filepath.Join(s.config.storageDir, envelope.UploadID)
	if err := os.Rename(stage, destination); err != nil {
		// Another process can publish first. Never replace a complete non-empty
		// directory; acknowledge its original receipt only for identical input.
		existing, readErr := s.readReceipt(envelope.UploadID)
		if readErr != nil {
			return uploadReceipt{}, false, err
		}
		if existing.CenterID != envelope.CenterID || existing.SHA256 != requestHash {
			return existing, false, errConflict
		}
		if verifyErr := s.verifyBundle(existing); verifyErr != nil {
			return existing, false, verifyErr
		}
		if syncErr := syncDirectory(s.config.storageDir); syncErr != nil {
			return existing, false, syncErr
		}
		return existing, false, nil
	}
	if err := syncDirectory(s.config.storageDir); err != nil {
		return receipt, true, err
	}
	return receipt, true, nil
}

func (s *supportService) openStoredFile(id, basename string) (*os.File, error) {
	if !uuidPattern.MatchString(id) {
		return nil, os.ErrNotExist
	}
	directory := filepath.Join(s.config.storageDir, id)
	info, err := os.Lstat(directory)
	if err != nil {
		return nil, err
	}
	if !info.IsDir() || info.Mode()&os.ModeSymlink != 0 {
		return nil, errors.New("invalid upload storage")
	}
	fileName := filepath.Join(directory, basename)
	info, err = os.Lstat(fileName)
	if err != nil {
		return nil, err
	}
	if !info.Mode().IsRegular() {
		return nil, errors.New("invalid upload storage")
	}
	return os.Open(fileName)
}

func (s *supportService) readReceipt(id string) (uploadReceipt, error) {
	var receipt uploadReceipt
	file, err := s.openStoredFile(id, "receipt.json")
	if err != nil {
		return receipt, err
	}
	defer file.Close()
	data, err := io.ReadAll(io.LimitReader(file, 8193))
	if err != nil || len(data) > 8192 {
		return receipt, errors.New("invalid receipt")
	}
	if err := json.Unmarshal(data, &receipt); err != nil {
		return receipt, err
	}
	if receipt.UploadID != id || !uuidPattern.MatchString(receipt.ReceiptID) ||
		!identifierPattern.MatchString(receipt.CenterID) || !hashPattern.MatchString(receipt.SHA256) ||
		!hashPattern.MatchString(receipt.BundleSHA256) || !validUTC(receipt.CreatedAt) ||
		!validUTC(receipt.ReceivedAt) || !validMetadata(receipt.App) || receipt.Size < 1 || receipt.Size > 1024*1024*1024 {
		return receipt, errors.New("invalid receipt")
	}
	return receipt, nil
}

func (s *supportService) verifyBundle(receipt uploadReceipt) error {
	file, err := s.openStoredFile(receipt.UploadID, "bundle.json")
	if err != nil {
		return err
	}
	defer file.Close()
	info, err := file.Stat()
	if err != nil || info.Size() != receipt.Size {
		return errors.New("incomplete bundle")
	}
	hash := sha256.New()
	if _, err := io.Copy(hash, file); err != nil {
		return err
	}
	if hex.EncodeToString(hash.Sum(nil)) != receipt.BundleSHA256 {
		return errors.New("incomplete bundle")
	}
	return nil
}
