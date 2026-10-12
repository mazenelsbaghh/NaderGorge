package main

import (
	"bufio"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

func TestAbandonedStudentConnectionsCloseAndLeaveJoiningAvailable(t *testing.T) {
	for _, interrupted := range []string{"request body", "idle keep-alive"} {
		t.Run(interrupted, func(t *testing.T) {
			db, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
			if err != nil {
				t.Fatal(err)
			}
			defer db.sql.Close()
			created, err := db.createExam(demoExam())
			if err != nil {
				t.Fatal(err)
			}
			if _, err := db.changeState(str(created["id"]), "publish"); err != nil {
				t.Fatal(err)
			}
			a := &app{db: db, addresses: func() []string { return nil }}
			server := httptest.NewUnstartedServer(nil)
			a.studentPort = server.Listener.Addr().(*net.TCPAddr).Port
			server.Config = a.server(false)
			// Accelerate the configured deadlines; an omitted bound stays zero.
			server.Config.ReadTimeout /= 150
			server.Config.IdleTimeout /= 150
			server.Start()
			defer server.Close()
			conn, err := net.DialTimeout("tcp", server.Listener.Addr().String(), time.Second)
			if err != nil {
				t.Fatal(err)
			}
			defer conn.Close()
			if err := conn.SetReadDeadline(time.Now().Add(2 * time.Second)); err != nil {
				t.Fatal(err)
			}
			reader := bufio.NewReader(conn)
			if interrupted == "request body" {
				_, err = fmt.Fprintf(conn, "POST /api/join HTTP/1.1\r\nHost: 127.0.0.1:%d\r\nContent-Type: application/json\r\nX-Exam-Request: 1\r\nContent-Length: 200\r\n\r\n{", a.studentPort)
			} else {
				_, err = fmt.Fprintf(conn, "GET /api/session HTTP/1.1\r\nHost: 127.0.0.1:%d\r\n\r\n", a.studentPort)
			}
			if err != nil {
				t.Fatal(err)
			}
			response, err := http.ReadResponse(reader, nil)
			if err != nil {
				t.Fatalf("abandoned %s was not answered within its bound: %v", interrupted, err)
			}
			_, err = io.Copy(io.Discard, response.Body)
			response.Body.Close()
			if err != nil {
				t.Fatal(err)
			}
			if interrupted == "request body" && response.StatusCode != http.StatusBadRequest {
				t.Fatalf("partial join unexpectedly returned %d", response.StatusCode)
			}
			if interrupted == "idle keep-alive" && response.StatusCode != http.StatusOK {
				t.Fatalf("initial session check returned %d", response.StatusCode)
			}
			if _, err = reader.ReadByte(); err != io.EOF {
				t.Fatalf("abandoned %s connection was retained: %v", interrupted, err)
			}
			if count := db.count(`SELECT COUNT(*) FROM attempts`); count != 0 {
				t.Fatal("partial request created a student attempt")
			}
			body := encode(M{"code": "CONNECTED", "name": "طالب اختبار", "phone": "01012345678"})
			request, err := http.NewRequest(http.MethodPost, server.URL+"/api/join", strings.NewReader(body))
			if err != nil {
				t.Fatal(err)
			}
			request.Header.Set("Content-Type", "application/json")
			request.Header.Set("X-Exam-Request", "1")
			joined, err := server.Client().Do(request)
			if err != nil {
				t.Fatalf("a later student could not reach the server: %v", err)
			}
			defer joined.Body.Close()
			if joined.StatusCode != http.StatusOK || db.count(`SELECT COUNT(*) FROM attempts WHERE joined_at IS NOT NULL`) != 1 {
				t.Fatalf("a later student could not join: HTTP %d", joined.StatusCode)
			}
		})
	}
}
