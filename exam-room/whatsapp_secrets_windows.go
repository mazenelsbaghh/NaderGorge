package main

import (
	"bytes"
	"encoding/json"
	"golang.org/x/sys/windows"
	"unsafe"
)

var whatsappSecretPrefix = []byte("MASSAR-DPAPI-1\n")

func protectWhatsAppConfig(contents []byte) ([]byte, error) {
	input := windows.DataBlob{Size: uint32(len(contents)), Data: &contents[0]}
	var output windows.DataBlob
	if err := windows.CryptProtectData(&input, nil, nil, 0, nil, windows.CRYPTPROTECT_UI_FORBIDDEN, &output); err != nil {
		return nil, fail("تعذر حماية حساب واتساب لمستخدم ويندوز الحالي", 500)
	}
	defer windows.LocalFree(windows.Handle(unsafe.Pointer(output.Data)))
	return append(append([]byte{}, whatsappSecretPrefix...), unsafe.Slice(output.Data, int(output.Size))...), nil
}
func unprotectWhatsAppConfig(contents []byte) ([]byte, error) {
	if !bytes.HasPrefix(contents, whatsappSecretPrefix) {
		return contents, nil
	} // Migrate legacy private JSON at the next explicit save.
	data := contents[len(whatsappSecretPrefix):]
	if len(data) == 0 {
		return nil, fail("ملف حساب واتساب غير صالح", 500)
	}
	input := windows.DataBlob{Size: uint32(len(data)), Data: &data[0]}
	var output windows.DataBlob
	if err := windows.CryptUnprotectData(&input, nil, nil, 0, nil, windows.CRYPTPROTECT_UI_FORBIDDEN, &output); err != nil {
		return nil, fail("أعد ربط حساب واتساب لمستخدم ويندوز الحالي", 500)
	}
	defer windows.LocalFree(windows.Handle(unsafe.Pointer(output.Data)))
	return append([]byte{}, unsafe.Slice(output.Data, int(output.Size))...), nil
}
func writeWhatsAppConfig(path string, config whatsappConfig) error {
	raw, err := json.Marshal(config)
	if err != nil {
		return err
	}
	protected, err := protectWhatsAppConfig(raw)
	if err != nil {
		return err
	}
	return writePrivateBytes(path, protected)
}
