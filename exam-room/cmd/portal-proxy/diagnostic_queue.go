package main

import (
	"bytes"
	"io"
	"log"
	"sync/atomic"
)

const diagnosticQueueLimit = 256

type queuedDiagnostics struct {
	records chan []byte
	dropped atomic.Uint64
}

func queueDiagnostics(sink io.Writer) *queuedDiagnostics {
	queue := &queuedDiagnostics{records: make(chan []byte, diagnosticQueueLimit)}
	go func() {
		for record := range queue.records {
			if _, err := sink.Write(record); err != nil {
				log.Printf("portal diagnostic write failed: %v", err)
			}
			if dropped := queue.dropped.Swap(0); dropped != 0 {
				log.Printf("portal diagnostic storage delayed; dropped %d records", dropped)
			}
		}
	}()
	return queue
}

func (queue *queuedDiagnostics) Write(record []byte) (int, error) {
	// Diagnostics are best effort: a stuck disk cannot hold student responses or
	// create an unbounded queue. The worker reports lost records when it resumes.
	select {
	case queue.records <- bytes.Clone(record):
	default:
		queue.dropped.Add(1)
	}
	return len(record), nil
}
