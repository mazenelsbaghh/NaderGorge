package main

import (
	"context"
	"errors"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"
)

func main() {
	if err := run(); err != nil {
		// Never print raw errors, request bodies, tokens, or filesystem paths.
		fmt.Fprintln(os.Stderr, "massar_support_start_failed")
		os.Exit(1)
	}
}

func run() error {
	c, err := readConfig()
	if err != nil {
		return err
	}
	service, err := newService(c)
	if err != nil {
		return err
	}
	server := &http.Server{
		Addr: c.listen, Handler: service,
		ReadHeaderTimeout: 10 * time.Second, ReadTimeout: 3 * time.Minute,
		WriteTimeout: 5 * time.Minute, IdleTimeout: 30 * time.Second,
		MaxHeaderBytes: 8 * 1024,
		ErrorLog:       log.New(discardLog{}, "", 0),
	}
	stopped := make(chan os.Signal, 1)
	signal.Notify(stopped, os.Interrupt, syscall.SIGTERM)
	defer signal.Stop(stopped)
	failure := make(chan error, 1)
	go func() { failure <- server.ListenAndServe() }()
	select {
	case err := <-failure:
		if errors.Is(err, http.ErrServerClosed) {
			return nil
		}
		return err
	case <-stopped:
		ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
		defer cancel()
		return server.Shutdown(ctx)
	}
}

type discardLog struct{}

func (discardLog) Write(data []byte) (int, error) { return len(data), nil }
