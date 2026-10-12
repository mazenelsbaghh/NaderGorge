package main

import (
	"errors"
	"sync"
	"testing"
	"time"
)

func TestExamSleepProtectionTracksRoomsAndRestoresOnShutdown(t *testing.T) {
	d, eid, _ := centerTestRoom(t)
	ticks, stop, done := make(chan time.Time), make(chan struct{}), make(chan struct{})
	states := make(chan bool, 10)
	failures := make(chan error, 10)
	go func() {
		defer close(done)
		monitorExamSleep(d, ticks, stop, func(active bool) error { states <- active; return nil }, func(err error) { failures <- err })
	}()
	var once sync.Once
	shutdown := func() { once.Do(func() { close(stop); <-done }) }
	t.Cleanup(shutdown)
	expectState := func(wanted bool) {
		t.Helper()
		select {
		case state := <-states:
			if state != wanted {
				t.Fatalf("sleep protection = %t, want %t", state, wanted)
			}
		case err := <-failures:
			t.Fatal(err)
		case <-time.After(5 * time.Second):
			t.Fatal("sleep protection did not follow the room lifecycle")
		}
	}
	expectState(true) // A waiting room must protect students already connected.
	if _, err := d.changeState(eid, "close"); err != nil {
		t.Fatal(err)
	}
	ticks <- time.Now()
	expectState(false)
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(str(created["id"]), "publish"); err != nil {
		t.Fatal(err)
	}
	ticks <- time.Now()
	expectState(true)
	shutdown()
	expectState(false) // Thread shutdown restores Windows' normal power behavior.
}

func TestExamSleepProtectionRetriesWindowsFailure(t *testing.T) {
	d, _, _ := centerTestRoom(t)
	ticks, stop, done := make(chan time.Time), make(chan struct{}), make(chan struct{})
	states, failures := make(chan bool, 10), make(chan error, 10)
	go func() {
		defer close(done)
		first := true
		monitorExamSleep(d, ticks, stop, func(active bool) error {
			if first {
				first = false
				return errors.New("synthetic Windows API failure")
			}
			states <- active
			return nil
		}, func(err error) { failures <- err })
	}()
	defer func() { close(stop); <-done }()
	select {
	case <-failures:
	case <-time.After(5 * time.Second):
		t.Fatal("native failure was not reported")
	}
	ticks <- time.Now()
	select {
	case active := <-states:
		if !active {
			t.Fatal("retry released an active room")
		}
	case <-time.After(5 * time.Second):
		t.Fatal("native failure was not retried")
	}
}
