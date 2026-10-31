package main

import (
	"net/http"
	"strconv"
)

// The browser stops editing at the deadline. Only delivery gets extra time;
// manual submission, cancellation and an explicit teacher close remain final.
const answerSyncGraceSeconds = 5 * 60

func studentSessionJSON(w http.ResponseWriter, r *http.Request, response M) {
	session, ok := response["session"].(M)
	if ok && session["state"] == "running" && r.URL.Query().Get("knownAttempt") == session["id"] {
		delete(session, "questions")
		revision, err := strconv.Atoi(r.URL.Query().Get("knownRevision"))
		if err == nil && revision == session["revision"] {
			delete(session, "answers")
		}
	}
	jsonOut(w, response)
}
