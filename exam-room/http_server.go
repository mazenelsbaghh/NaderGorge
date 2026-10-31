package main

import (
	"net/http"
	"time"
)

func (a *app) server(admin bool) *http.Server {
	server := &http.Server{
		Handler:           a.handler(admin),
		ReadHeaderTimeout: 12 * time.Second,
		ReadTimeout:       15 * time.Second,
		IdleTimeout:       30 * time.Second,
	}
	// The local admin server also generates PDF bundles, which can take minutes.
	if !admin {
		server.WriteTimeout = 30 * time.Second
	}
	return server
}
