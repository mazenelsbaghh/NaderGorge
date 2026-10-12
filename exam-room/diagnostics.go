package main

import (
	"bufio"
	"context"
	"encoding/json"
	"io"
	"os"
	"path/filepath"
	"sort"
	"sync"
	"time"
)

const (
	diagnosticFileBytes  = 2 * 1024 * 1024
	diagnosticEventLimit = 8192
	diagnosticQueueSize  = 256
)

// Only controlled classifications enter this structure. In particular it has
// no arbitrary message, URL, identity, address, headers, or request body field.
type diagnosticEvent struct {
	ReceivedAt string `json:"receivedAt"`
	Source     string `json:"source"`
	Kind       string `json:"kind"`
	Class      string `json:"class,omitempty"`
	Status     int    `json:"status"`
	DurationMS int64  `json:"durationMs,omitempty"`
	Category   string `json:"category,omitempty"`
	ClientTime string `json:"clientTime,omitempty"`
	Count      uint64 `json:"count,omitempty"`
}

type diagnosticCount struct {
	Class           string `json:"class"`
	StatusGroup     string `json:"statusGroup"`
	Count           uint64 `json:"count"`
	TotalDurationMS int64  `json:"totalDurationMs"`
	MaxDurationMS   int64  `json:"maxDurationMs"`
}

type diagnosticRecorder struct {
	mu                    sync.Mutex
	queue                 chan diagnosticEvent
	done                  chan struct{}
	closed                bool
	events                []diagnosticEvent
	eventStart            int
	historyEvicted        uint64
	counts                map[string]diagnosticCount
	interval              map[string]diagnosticCount
	since                 string
	dropped               uint64
	writeFailures         uint64
	persistence           string
	lastWriteAt           string
	sink                  io.WriteCloser
	openSink              func() (io.WriteCloser, error)
	clientRejectedBatches uint64
	clients               clientDiagnosticLimits
}

func openDiagnosticRecorder(directory string) *diagnosticRecorder {
	sink, err := openDiagnosticFile(directory, diagnosticFileBytes)
	var writer io.WriteCloser
	if err == nil {
		writer = sink
	}
	recorder := newDiagnosticRecorder(writer)
	recorder.openSink = func() (io.WriteCloser, error) { return openDiagnosticFile(directory, diagnosticFileBytes) }
	if err != nil {
		recorder.persistence = "unavailable"
		recorder.writeFailures = 1
	} else {
		// Loading is startup-only, bounded by the same two-file retention limit.
		for _, name := range []string{"backend-events.jsonl.1", "backend-events.jsonl"} {
			file, err := os.Open(filepath.Join(directory, name))
			if err != nil {
				continue
			}
			scanner := bufio.NewScanner(io.LimitReader(file, diagnosticFileBytes))
			scanner.Buffer(make([]byte, 4096), 4096)
			for scanner.Scan() {
				var event diagnosticEvent
				if json.Unmarshal(scanner.Bytes(), &event) == nil && validStoredDiagnostic(event) {
					recorder.appendLocked(event)
				}
			}
			file.Close()
		}
	}
	go recorder.run()
	recorder.record(diagnosticEvent{Source: "backend", Kind: "startup"})
	return recorder
}

// Worker startup is separate so tests can supply a real blocking I/O boundary.
func newDiagnosticRecorder(sink io.WriteCloser) *diagnosticRecorder {
	return &diagnosticRecorder{queue: make(chan diagnosticEvent, diagnosticQueueSize), done: make(chan struct{}),
		counts: map[string]diagnosticCount{}, interval: map[string]diagnosticCount{},
		since: time.Now().UTC().Format(time.RFC3339Nano), persistence: "ready", sink: sink}
}

func (d *diagnosticRecorder) appendLocked(event diagnosticEvent) {
	if len(d.events) == diagnosticEventLimit {
		d.events[d.eventStart] = event
		d.eventStart = (d.eventStart + 1) % diagnosticEventLimit
		d.historyEvicted++
		return
	}
	d.events = append(d.events, event)
}

func (d *diagnosticRecorder) enqueueLocked(event diagnosticEvent) {
	if d.closed {
		return
	}
	event.ReceivedAt = time.Now().UTC().Format(time.RFC3339Nano)
	d.appendLocked(event)
	select {
	case d.queue <- event:
	default:
		d.dropped++
	}
}

func (d *diagnosticRecorder) record(event diagnosticEvent) {
	if d == nil {
		return
	}
	d.mu.Lock()
	defer d.mu.Unlock()
	d.enqueueLocked(event)
}

func (d *diagnosticRecorder) request(event diagnosticEvent) {
	if d == nil {
		return
	}
	d.mu.Lock()
	defer d.mu.Unlock()
	group := diagnosticStatusGroup(event.Status)
	key := event.Class + ":" + group
	for _, counts := range []map[string]diagnosticCount{d.counts, d.interval} {
		count := counts[key]
		count.Class, count.StatusGroup = event.Class, group
		count.Count++
		count.TotalDurationMS += event.DurationMS
		if event.DurationMS > count.MaxDurationMS {
			count.MaxDurationMS = event.DurationMS
		}
		counts[key] = count
	}
	if event.Status >= 400 || event.DurationMS >= 2000 || event.Category != "" {
		d.enqueueLocked(event)
	}
}

func (d *diagnosticRecorder) summariesLocked() {
	for _, count := range d.interval {
		d.enqueueLocked(diagnosticEvent{Source: "backend", Kind: "summary", Class: count.Class,
			Category: count.StatusGroup, Count: count.Count, DurationMS: count.MaxDurationMS})
	}
	d.interval = map[string]diagnosticCount{}
}

func (d *diagnosticRecorder) run() {
	defer close(d.done)
	defer func() {
		if d.sink != nil {
			d.sink.Close()
		}
	}()
	nextOpen := time.Now()
	ticker := time.NewTicker(time.Minute)
	defer ticker.Stop()
	for {
		select {
		case event, ok := <-d.queue:
			if !ok {
				return
			}
			var err error
			if d.sink == nil && d.openSink != nil && !time.Now().Before(nextOpen) {
				var opened io.WriteCloser
				opened, err = d.openSink()
				if err == nil {
					d.sink = opened
				}
				nextOpen = time.Now().Add(30 * time.Second)
			}
			if d.sink == nil {
				err = os.ErrInvalid
			} else {
				err = json.NewEncoder(d.sink).Encode(event)
			}
			d.mu.Lock()
			if err != nil {
				d.writeFailures++
				d.persistence = "unavailable"
			} else {
				d.persistence = "ready"
				d.lastWriteAt = time.Now().UTC().Format(time.RFC3339Nano)
			}
			d.mu.Unlock()
		case <-ticker.C:
			d.mu.Lock()
			d.summariesLocked()
			d.mu.Unlock()
		}
	}
}

func (d *diagnosticRecorder) close(ctx context.Context) bool {
	if d == nil {
		return true
	}
	d.mu.Lock()
	if !d.closed {
		d.summariesLocked()
		d.enqueueLocked(diagnosticEvent{Source: "backend", Kind: "shutdown"})
		d.closed = true
		close(d.queue)
	}
	d.mu.Unlock()
	select {
	case <-d.done:
		return true
	case <-ctx.Done():
		return false
	}
}

func (d *diagnosticRecorder) startupFailed(err error) string {
	category := diagnosticErrorCategory(err)
	d.record(diagnosticEvent{Source: "backend", Kind: "startup_failed", Category: category})
	// log.Fatal bypasses deferred cleanup. Give this one failure the same bounded
	// drain as normal shutdown without letting an unavailable disk hang startup.
	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()
	d.close(ctx)
	return category
}

func (d *diagnosticRecorder) snapshot() M {
	result := M{"schemaVersion": 1, "capturedAt": time.Now().UTC().Format(time.RFC3339Nano), "available": d != nil,
		"clientTimeUntrusted": true, "events": []diagnosticEvent{}}
	if d == nil {
		return result
	}
	d.mu.Lock()
	defer d.mu.Unlock()
	events := make([]diagnosticEvent, len(d.events))
	copied := copy(events, d.events[d.eventStart:])
	copy(events[copied:], d.events[:d.eventStart])
	counts := make([]diagnosticCount, 0, len(d.counts))
	for _, count := range d.counts {
		counts = append(counts, count)
	}
	sort.Slice(counts, func(i, j int) bool {
		return counts[i].Class+counts[i].StatusGroup < counts[j].Class+counts[j].StatusGroup
	})
	result["events"] = events
	result["recorder"] = M{"persistence": d.persistence, "queued": len(d.queue), "dropped": d.dropped,
		"writeFailures": d.writeFailures, "lastWriteAt": d.lastWriteAt, "maxFiles": 2, "maxFileBytes": diagnosticFileBytes,
		"historyLimit": diagnosticEventLimit, "historyEvicted": d.historyEvicted, "clientRejectedBatches": d.clientRejectedBatches}
	result["summary"] = M{"since": d.since, "requests": counts}
	return result
}

type diagnosticFile struct {
	path  string
	file  *os.File
	size  int64
	limit int64
}

func openDiagnosticFile(directory string, limit int64) (*diagnosticFile, error) {
	if err := os.MkdirAll(directory, 0700); err != nil {
		return nil, err
	}
	path := filepath.Join(directory, "backend-events.jsonl")
	file, err := os.OpenFile(path, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0600)
	if err != nil {
		return nil, err
	}
	info, err := file.Stat()
	if err != nil {
		file.Close()
		return nil, err
	}
	return &diagnosticFile{path: path, file: file, size: info.Size(), limit: limit}, nil
}
func (d *diagnosticFile) Write(data []byte) (int, error) {
	if d.file == nil {
		file, err := os.OpenFile(d.path, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0600)
		if err != nil {
			return 0, err
		}
		info, err := file.Stat()
		if err != nil {
			file.Close()
			return 0, err
		}
		d.file = file
		d.size = info.Size()
	}
	if d.size+int64(len(data)) > d.limit {
		if err := d.file.Close(); err != nil {
			return 0, err
		}
		d.file = nil
		if err := os.Rename(d.path, d.path+".1"); err != nil {
			return 0, err
		}
		file, err := os.OpenFile(d.path, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0600)
		if err != nil {
			return 0, err
		}
		d.file = file
		d.size = 0
	}
	n, err := d.file.Write(data)
	d.size += int64(n)
	return n, err
}
func (d *diagnosticFile) Close() error {
	if d.file == nil {
		return nil
	}
	return d.file.Close()
}
