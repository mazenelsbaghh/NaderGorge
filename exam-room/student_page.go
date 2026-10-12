package main

import (
	"bytes"
	"compress/gzip"
	"crypto/sha256"
	"encoding/base64"
	"fmt"
	"net/http"
	"strconv"
	"strings"
	"sync"
)

type studentDocument struct {
	html, compressed []byte
	policy           string
}

var bundledStudentDocument = sync.OnceValues(buildStudentDocument)

func inlineAsset(name, tag string) (string, string, error) {
	body, err := webFiles.ReadFile("web/assets/" + name)
	if err != nil {
		return "", "", err
	}
	if bytes.Contains(bytes.ToLower(body), []byte("</"+tag)) {
		return "", "", fmt.Errorf("embedded %s contains a closing %s tag", name, tag)
	}
	digest := sha256.Sum256(body)
	hash := "'sha256-" + base64.StdEncoding.EncodeToString(digest[:]) + "'"
	return "<" + tag + ">" + string(body) + "</" + tag + ">", hash, nil
}

func buildStudentDocument() (studentDocument, error) {
	template, err := webFiles.ReadFile("web/student.html")
	if err != nil {
		return studentDocument{}, err
	}
	style, styleHash, err := inlineAsset("style.css", "style")
	if err != nil {
		return studentDocument{}, err
	}
	script, scriptHash, err := inlineAsset("student-compat.js", "script")
	if err != nil {
		return studentDocument{}, err
	}
	// One response avoids partially loaded pages when Wi-Fi drops between asset requests.
	html := strings.Replace(string(template), "<!-- student-style -->", style, 1)
	html = strings.Replace(html, "<!-- student-script -->", script, 1)
	var compressed bytes.Buffer
	writer := gzip.NewWriter(&compressed)
	if _, err = writer.Write([]byte(html)); err != nil {
		return studentDocument{}, err
	}
	if err = writer.Close(); err != nil {
		return studentDocument{}, err
	}
	policy := "default-src 'self'; script-src 'self' " + scriptHash + "; style-src 'self' " + styleHash + "; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'"
	return studentDocument{html: []byte(html), compressed: compressed.Bytes(), policy: policy}, nil
}

func acceptsStudentGzip(accept string) bool {
	for _, coding := range strings.Split(accept, ",") {
		parts := strings.Split(coding, ";")
		if strings.TrimSpace(parts[0]) != "gzip" {
			continue
		}
		for _, parameter := range parts[1:] {
			key, value, found := strings.Cut(strings.TrimSpace(parameter), "=")
			if found && strings.TrimSpace(key) == "q" {
				quality, err := strconv.ParseFloat(strings.TrimSpace(value), 64)
				return err == nil && quality > 0 && quality <= 1
			}
		}
		return true
	}
	return false
}

func serveStudentDocument(w http.ResponseWriter, r *http.Request) error {
	document, err := bundledStudentDocument()
	if err != nil {
		return err
	}
	body := document.html
	if acceptsStudentGzip(r.Header.Get("Accept-Encoding")) {
		body = document.compressed
		w.Header().Set("Content-Encoding", "gzip")
	}
	w.Header().Set("Vary", "Accept-Encoding")
	w.Header().Set("Content-Security-Policy", document.policy)
	w.Header().Set("Content-Length", strconv.Itoa(len(body)))
	send(w, body, "text/html; charset=utf-8")
	return nil
}
