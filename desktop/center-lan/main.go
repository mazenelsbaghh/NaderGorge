package main

import (
	"encoding/json"
	"io"
	"os"
	"os/signal"
	"syscall"
)

func main() {
	if err := run(); err != nil {
		json.NewEncoder(os.Stdout).Encode(map[string]any{"ready": false, "error": "gateway_start_failed"})
		os.Exit(1)
	}
}

func run() error {
	config, err := readConfig(os.Stdin)
	if err != nil {
		return err
	}
	gateway, err := newGateway(config)
	if err != nil {
		return err
	}
	ready, err := gateway.start()
	if err != nil {
		return err
	}
	defer gateway.close()
	if err = json.NewEncoder(os.Stdout).Encode(ready); err != nil {
		return err
	}
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	defer signal.Stop(stop)
	parentClosed := make(chan struct{})
	go func() {
		// EOF also covers a crashed parent, so the LAN listener cannot outlive Flutter.
		io.Copy(io.Discard, os.Stdin)
		close(parentClosed)
	}()
	select {
	case <-stop:
	case <-parentClosed:
	}
	return nil
}
