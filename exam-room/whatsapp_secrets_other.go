//go:build !windows

package main

func unprotectWhatsAppConfig(contents []byte) ([]byte, error) { return contents, nil }
func writeWhatsAppConfig(path string, config whatsappConfig) error {
	return writePrivateJSON(path, config)
}
