package main

import (
	"context"
	"embed"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"io"
	"log"
	"mime"
	"net"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"syscall"
	"time"
)

//go:embed web
var webFiles embed.FS

type app struct {
	db                     *DB
	grader                 *grader
	adminToken             string
	studentPort, adminPort int
	addresses              func() []string
	pdfHelper              string
	whatsapp               *whatsappCloud
	diagnostics            *diagnosticRecorder
	centerIntegrated       bool
	centerAccess           centerBrowserAccess
}

func (a *app) adminOrigin() string { return fmt.Sprintf("http://127.0.0.1:%d", a.adminPort) }

func localAddresses() []string {
	seen := map[string]bool{}
	out := []string{}
	interfaces, _ := net.Interfaces()
	for _, iface := range interfaces {
		if iface.Flags&net.FlagUp == 0 || iface.Flags&net.FlagLoopback != 0 || iface.Flags&net.FlagPointToPoint != 0 {
			continue
		}
		addrs, _ := iface.Addrs()
		for _, entry := range addrs {
			var ip net.IP
			switch v := entry.(type) {
			case *net.IPNet:
				ip = v.IP
			case *net.IPAddr:
				ip = v.IP
			}
			if ip4 := ip.To4(); ip4 != nil && !ip4.IsLoopback() && !ip4.IsLinkLocalUnicast() {
				address := ip4.String()
				if !seen[address] {
					seen[address] = true
					out = append(out, address)
				}
			}
		}
	}
	return out
}
func (a *app) studentURLs() []string {
	urls := []string{}
	for _, address := range a.addresses() {
		if address == "10.77.0.1" {
			urls = append([]string{"http://10.77.0.1/"}, urls...)
		}
		urls = append(urls, fmt.Sprintf("http://%s:%d", address, a.studentPort))
	}
	return urls
}
func (a *app) handler(admin bool) http.Handler {
	handler := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if admin {
			// pywebview creates the native bridge functions with new Function.
			// Keep this exception on the loopback-only admin server.
			w.Header().Set("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
		}
		if e := a.verify(r, admin); e != nil {
			writeErr(w, e)
			return
		}
		if strings.HasPrefix(r.URL.Path, "/api/") || strings.HasPrefix(r.URL.Path, "/report/") {
			var e error
			if admin {
				e = a.adminAPI(w, r)
			} else {
				e = a.studentAPI(w, r)
			}
			if e != nil {
				if diagnostic, ok := w.(interface{ diagnosticError(error) }); ok {
					diagnostic.diagnosticError(e)
				}
				writeErr(w, sqlErr(e))
			}
			return
		}
		if e := serveWeb(w, r, admin); e != nil {
			writeErr(w, e)
		}
	})
	return a.diagnostics.middleware(handler, admin)
}
func (a *app) verify(r *http.Request, admin bool) error {
	host, port, e := net.SplitHostPort(r.Host)
	if e != nil {
		return fail("عنوان الشبكة غير صالح", 403)
	}
	expected := a.studentPort
	if admin {
		expected = a.adminPort
	}
	if port != strconv.Itoa(expected) {
		return fail("عنوان الشبكة غير مسموح", 403)
	}
	allowed := host == "localhost" || host == "127.0.0.1"
	if !admin {
		for _, address := range a.addresses() {
			if host == address {
				allowed = true
			}
		}
	}
	if !allowed {
		return fail("عنوان الشبكة غير مسموح", 403)
	}
	if origin := r.Header.Get("Origin"); origin != "" && origin != "http://"+r.Host {
		return fail("مصدر الطلب غير مسموح", 403)
	}
	if r.Method != "GET" && r.Method != "POST" {
		return fail("الصفحة غير موجودة", 404)
	}
	if r.Method == "POST" {
		if r.Header.Get("X-Exam-Request") != "1" {
			return fail("طلب غير مسموح", 403)
		}
		if strings.Split(r.Header.Get("Content-Type"), ";")[0] != "application/json" {
			return fail("صيغة الطلب غير مدعومة", 415)
		}
	}
	return nil
}
func headers(w http.ResponseWriter, contentType string) {
	h := w.Header()
	h.Set("Content-Type", contentType)
	h.Set("Cache-Control", "no-store")
	h.Set("X-Content-Type-Options", "nosniff")
	h.Set("Referrer-Policy", "no-referrer")
	if h.Get("Content-Security-Policy") == "" {
		h.Set("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")
	}
}
func send(w http.ResponseWriter, body []byte, contentType string) {
	headers(w, contentType)
	_, _ = w.Write(body)
}
func jsonOut(w http.ResponseWriter, v any) {
	send(w, []byte(encode(v)), "application/json; charset=utf-8")
}
func writeErr(w http.ResponseWriter, e error) {
	if diagnostic, ok := w.(interface{ diagnosticError(error) }); ok {
		diagnostic.diagnosticError(e)
	}
	status := 500
	message := "تعذر إكمال العملية. لم يتم تأكيد الحفظ؛ حاول مجددًا وراجع سجل التشغيل."
	var room roomError
	if errors.As(e, &room) {
		status = room.status
		message = room.msg
	}
	headers(w, "application/json; charset=utf-8")
	w.WriteHeader(status)
	_, _ = w.Write([]byte(encode(M{"error": message})))
}
func payload(r *http.Request) (M, error) {
	if r.ContentLength <= 0 || r.ContentLength > 1500000 || len(r.TransferEncoding) > 0 {
		return nil, fail("حجم الطلب غير مسموح", 413)
	}
	var p M
	dec := json.NewDecoder(io.LimitReader(r.Body, 1500001))
	if e := dec.Decode(&p); e != nil || p == nil {
		return nil, fail("تعذر قراءة الطلب", 400)
	}
	var extra any
	if dec.Decode(&extra) != io.EOF {
		return nil, fail("صيغة الطلب غير صالحة", 400)
	}
	return p, nil
}
func cookie(r *http.Request, name string) string {
	c, e := r.Cookie(name)
	if e != nil {
		return ""
	}
	return c.Value
}
func (a *app) studentAPI(w http.ResponseWriter, r *http.Request) error {
	p := r.URL.Path
	tok := cookie(r, "massar_student")
	switch {
	case r.Method == "POST" && p == "/api/client-diagnostics":
		return a.clientDiagnostics(w, r)
	case r.Method == "GET" && p == "/api/lounge":
		v, e := a.db.lounge()
		if e == nil && a.centerIntegrated {
			v["centerIntegrated"] = true
			rooms := []M{}
			for _, room := range v["rooms"].([]M) {
				if _, err := readCenterBinding(a.db.sql, str(room["id"])); err == nil {
					rooms = append(rooms, room)
				}
			}
			v["rooms"] = rooms
			v["exam"] = nil
		}
		if e == nil {
			jsonOut(w, v)
		}
		return e
	case r.Method == "GET" && p == "/api/session":
		v, e := a.db.studentPresence(tok, r.URL.Query().Get("visible") != "0")
		// A delayed expired response must not clear a newer login cookie.
		if e == nil {
			jsonOut(w, v)
		}
		return e
	case r.Method == "POST" && p == "/api/presence":
		body, err := payload(r)
		if err != nil {
			return err
		}
		visible, err := boolField(body, "visible")
		if err != nil {
			return err
		}
		screen, _ := body["screen"].(map[string]any)
		v, err := a.db.studentPresence(tok, visible, M(screen))
		if err == nil {
			jsonOut(w, v)
		}
		return err
	case r.Method == "POST" && p == "/api/center/lookup":
		body, e := payload(r)
		if e != nil {
			return e
		}
		v, e := a.db.lookupCenter(body)
		if e == nil {
			jsonOut(w, v)
		}
		return e
	case r.Method == "POST" && p == "/api/join":
		body, e := payload(r)
		if e != nil {
			return e
		}
		if a.centerIntegrated {
			eid, err := centerExamID(a.db.sql, body)
			if err != nil {
				return err
			}
			if _, err = readCenterBinding(a.db.sql, eid); err != nil {
				return fail("القاعة لم ترتبط بكشف السنتر بعد", 409)
			}
			body["examId"] = eid
		}
		v, newToken, e := a.db.join(body, tok)
		if e == nil {
			http.SetCookie(w, &http.Cookie{Name: "massar_student", Value: newToken, Path: "/", HttpOnly: true, SameSite: http.SameSiteStrictMode, MaxAge: 604800})
			jsonOut(w, v)
		}
		return e
	case r.Method == "POST" && p == "/api/answers":
		body, e := payload(r)
		if e != nil {
			return e
		}
		v, e := a.db.saveAnswers(tok, body)
		if e == nil {
			jsonOut(w, v)
		}
		return e
	case r.Method == "POST" && p == "/api/leave":
		if _, e := payload(r); e != nil {
			return e
		}
		v, e := a.db.session(tok)
		if e != nil {
			return e
		}
		if session := v["session"]; session != nil && session.(M)["state"] != "submitted" {
			return fail("سلّم المحاولة الحالية قبل الدخول لامتحان آخر", 409)
		}
		http.SetCookie(w, &http.Cookie{Name: "massar_student", Path: "/", HttpOnly: true, SameSite: http.SameSiteStrictMode, MaxAge: -1})
		jsonOut(w, M{"session": nil})
		return nil
	}
	return fail("الصفحة غير موجودة", 404)
}
func (a *app) adminAPI(w http.ResponseWriter, r *http.Request) error {
	path := r.URL.Path
	parts := strings.Split(path, "/")

	if a.centerIntegrated && r.Method == "POST" && path == "/api/center/browser-login" {
		return a.centerBrowserLogin(w, r)
	}
	if r.Method == "GET" && path == "/api/bootstrap" {
		uiToken := a.adminToken
		if a.centerIntegrated {
			uiToken = cookie(r, "massar_admin")
			if !a.browserTokenValid(uiToken) {
				return fail("افتح شاشة الامتحانات من برنامج السنتر بعد تسجيل الدخول", 401)
			}
		}
		http.SetCookie(w, &http.Cookie{Name: "massar_admin", Value: uiToken, Path: "/", HttpOnly: true, SameSite: http.SameSiteStrictMode})
		jsonOut(w, M{"token": uiToken, "centerIntegrated": a.centerIntegrated, "studentUrls": a.studentURLs(), "localStudentUrl": fmt.Sprintf("http://127.0.0.1:%d", a.studentPort)})
		return nil
	}
	presented := r.Header.Get("X-Admin-Token")
	if r.Method == "GET" && presented == "" {
		presented = cookie(r, "massar_admin")
	}
	controller := sameSecret(presented, a.adminToken)
	if !controller && !(a.centerIntegrated && a.browserTokenValid(presented)) {
		return fail("حدّث لوحة الإدارة لإعادة الاتصال", 401)
	}
	if strings.HasPrefix(path, "/api/center/") {
		if !controller {
			return fail("هذه العملية متاحة لبرنامج السنتر فقط", 403)
		}
		if r.Method == "GET" && path == "/api/center/state" {
			v, e := a.db.centerState()
			if e == nil {
				a.renewCenterBrowserLease()
				jsonOut(w, v)
			}
			return e
		}
		if r.Method == "POST" {
			body, e := payload(r)
			if e != nil {
				return e
			}
			var v M
			switch path {
			case "/api/center/open":
				v, e = a.db.openCenterRoom(body)
			case "/api/center/bind":
				v, e = a.db.bindCenter(body)
			case "/api/center/ack":
				v, e = a.db.ackCenter(body)
			case "/api/center/admin-access":
				v, e = a.centerAdminAccess(body)
			default:
				return fail("الصفحة غير موجودة", 404)
			}
			if e == nil {
				jsonOut(w, v)
			}
			return e
		}
		return fail("الصفحة غير موجودة", 404)
	}
	if r.Method == "GET" {
		switch {
		case path == "/api/diagnostics":
			jsonOut(w, a.diagnostics.snapshot())
			return nil
		case path == "/api/whatsapp/configuration":
			v, e := a.whatsapp.settings()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/whatsapp/templates":
			v, e := a.whatsapp.currentTemplates(r.Context())
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/whatsapp/jobs":
			v, e := a.whatsapp.jobs()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/whatsapp/history":
			v, e := a.whatsapp.history()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/status":
			jsonOut(w, M{"serverTime": now(), "studentUrls": a.studentURLs()})
			return nil
		case path == "/api/catalog":
			v, e := a.db.catalog()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/templates":
			v, e := a.db.templates()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/settings/rooms":
			v, e := a.db.roomSettings()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/settings/whatsapp":
			v, e := a.db.whatsappSettings()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/storage":
			v, e := a.db.storage()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case path == "/api/settings/gemini":
			key, e := a.grader.key()
			if e == nil {
				jsonOut(w, M{"configured": validKey(key), "path": a.grader.envPath})
			}
			return e
		case len(parts) == 5 && parts[1] == "api" && parts[2] == "exams" && parts[4] == "essay-batch":
			v, e := a.grader.status(parts[3])
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case len(parts) == 4 && parts[1] == "api" && parts[2] == "backups":
			p, e := a.db.backupFile(parts[3])
			if e != nil {
				return e
			}
			return download(w, p)
		case path == "/api/exams":
			v, e := a.db.listExams()
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case len(parts) == 4 && parts[1] == "api" && parts[2] == "exams":
			v, e := a.db.dashboard(parts[3])
			if e == nil {
				jsonOut(w, v)
			}
			return e
		case len(parts) == 5 && parts[1] == "api" && parts[2] == "exams":
			v, e := a.db.dashboard(parts[3])
			if e != nil {
				return e
			}
			if parts[4] == "reports.zip" {
				contents, err := a.reportsZIP(parts[3])
				if err != nil {
					return err
				}
				w.Header().Set("Content-Disposition", mime.FormatMediaType("attachment", map[string]string{"filename": "تقارير-" + str(v["exam"].(M)["config"].(M)["title"]) + ".zip"}))
				send(w, contents, "application/zip")
				return nil
			}
			if parts[4] == "results.xlsx" {
				labels, err := a.db.sheetContext(parts[3], r.URL.Query().Get("groupId"))
				if err != nil {
					return err
				}
				data, err := excelExport(v, labels)
				if err != nil {
					return err
				}
				w.Header().Set("Content-Disposition", mime.FormatMediaType("attachment", map[string]string{"filename": "نتائج-" + labels[2] + ".xlsx"}))
				send(w, data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
				return nil
			}
			if parts[4] != "students.csv" && parts[4] != "results.csv" {
				return fail("الصفحة غير موجودة", 404)
			}
			w.Header().Set("Content-Disposition", `attachment; filename="`+parts[4]+`"`)
			send(w, csvExport(v, parts[4] == "results.csv"), "text/csv; charset=utf-8")
			return nil
		case len(parts) == 5 && parts[1] == "api" && parts[2] == "attempts" && parts[4] == "report.pdf":
			v, err := a.db.report(parts[3])
			if err != nil {
				return err
			}
			data, err := a.reportPDF(v)
			if err != nil {
				return err
			}
			w.Header().Set("Content-Disposition", mime.FormatMediaType("attachment", map[string]string{"filename": "تقرير-" + str(v["attempt"].(M)["name"]) + ".pdf"}))
			send(w, data, "application/pdf")
			return nil
		case len(parts) == 3 && parts[1] == "report":
			v, e := a.db.report(parts[2])
			if e == nil {
				send(w, reportHTML(v), "text/html; charset=utf-8")
			}
			return e
		}
		return fail("الصفحة غير موجودة", 404)
	}
	body, e := payload(r)
	if e != nil {
		return e
	}
	var v M
	switch {
	case path == "/api/whatsapp/configuration":
		v, e = a.whatsapp.saveConfig(body)
	case path == "/api/whatsapp/templates/sync":
		v, e = a.whatsapp.syncTemplates(r.Context())
	case path == "/api/whatsapp/reports/preview":
		a.whatsapp.mu.Lock()
		_, e = a.whatsapp.ensureTemplatesLocked(r.Context())
		if e == nil {
			v, e = a.whatsapp.preview(body)
		}
		a.whatsapp.mu.Unlock()
	case path == "/api/whatsapp/jobs":
		v, e = a.whatsapp.enqueueReports(r.Context(), body)
	case path == "/api/whatsapp/jobs/resume":
		v, e = a.whatsapp.resumeJob(str(body["jobId"]))
	case path == "/api/whatsapp/jobs/stop":
		v, e = a.whatsapp.stopJob(str(body["jobId"]))
	case path == "/api/whatsapp/reports/send":
		v, e = a.whatsapp.sendReport(r.Context(), body)
	case path == "/api/templates":
		v, e = a.db.createTemplate(body)
	case len(parts) == 5 && parts[1] == "api" && parts[2] == "templates" && parts[4] == "save":
		v, e = a.db.saveTemplate(parts[3], body)
	case path == "/api/room/open":
		v, e = a.db.openRoom(str(body["lessonId"]), str(body["templateId"]))
	case path == "/api/network/maintenance/acquire":
		v, e = a.db.acquireNetworkMaintenance()
	case path == "/api/network/maintenance/release":
		v, e = a.db.releaseNetworkMaintenance(str(body["leaseToken"]))
	case len(parts) == 4 && parts[1] == "api" && parts[2] == "catalog":
		v, e = a.db.createCatalogItem(parts[3], body)
	case len(parts) == 6 && parts[1] == "api" && parts[2] == "catalog" && parts[5] == "rename":
		v, e = a.db.renameCatalogItem(parts[3], parts[4], body)
	case path == "/api/settings/rooms":
		v, e = a.db.saveRoomSettings(body)
	case path == "/api/settings/whatsapp":
		v, e = a.db.saveWhatsAppSettings(body)
	case path == "/api/exams":
		v, e = a.db.createExam(body)
	case path == "/api/demo":
		v, e = a.db.createExam(demoExam())
	case path == "/api/storage/check":
		v, e = a.db.check()
	case path == "/api/storage/open-folder":
		v, e = a.db.openFolder()
	case path == "/api/storage/backup":
		var p string
		p, e = a.db.backup()
		if e == nil {
			v = M{"name": filepath.Base(p)}
		}
	case path == "/api/settings/gemini":
		e = a.grader.saveKey(str(body["apiKey"]))
		if e == nil {
			v = M{"configured": true, "path": a.grader.envPath}
		}
	case len(parts) == 5 && parts[1] == "api" && parts[2] == "exams" && parts[4] == "essay-batch":
		v, e = a.grader.start(parts[3], body)
	case len(parts) == 6 && parts[1] == "api" && parts[2] == "exams" && parts[4] == "essay-batch" && parts[5] == "stop":
		v, e = a.grader.stop(parts[3])
	case path == "/api/backup":
		var p string
		p, e = a.db.backup()
		if e == nil {
			return download(w, p)
		}
	case len(parts) == 5 && parts[1] == "api" && parts[2] == "exams":
		switch parts[4] {
		case "screen-rule":
			v, e = a.db.screenRule(parts[3], body)
		case "absence-rule":
			v, e = a.db.absenceRule(parts[3], body)
		case "extend-time":
			v, e = a.db.extendTime(parts[3], body)
		case "save":
			v, e = a.db.updateExam(parts[3], body)
		case "duplicate":
			v, e = a.db.duplicateExam(parts[3])
		case "publish", "start", "close":
			v, e = a.db.changeStateForCenter(parts[3], parts[4], a.centerIntegrated)
		default:
			return fail("إجراء غير موجود", 404)
		}
	case len(parts) == 5 && parts[1] == "api" && parts[2] == "attempts":
		switch parts[4] {
		case "cancel":
			v, e = a.db.cancelAttempt(parts[3], body)
		case "recover-answers":
			v, e = a.db.recoverAnswers(parts[3], body)
		case "grade":
			v, e = a.db.gradeEssay(parts[3], body)
		case "pause", "resume":
			v, e = a.db.setPause(parts[3], parts[4] == "pause")
		case "reset-login":
			v, e = a.db.resetLogin(parts[3])
		default:
			return fail("إجراء غير موجود", 404)
		}
	default:
		return fail("إجراء غير موجود", 404)
	}
	if e == nil {
		jsonOut(w, v)
	}
	return e
}
func download(w http.ResponseWriter, path string) error {
	f, e := os.Open(path)
	if e != nil {
		return e
	}
	defer f.Close()
	headers(w, "application/octet-stream")
	w.Header().Set("Content-Disposition", `attachment; filename="`+filepath.Base(path)+`"`)
	_, e = io.Copy(w, f)
	return e
}
func serveWeb(w http.ResponseWriter, r *http.Request, admin bool) error {
	if r.Method != "GET" {
		return fail("الصفحة غير موجودة", 404)
	}
	path := ""
	if r.URL.Path == "/" {
		if !admin {
			return serveStudentDocument(w, r)
		}
		path = "web/admin.html"
	} else if strings.HasPrefix(r.URL.Path, "/assets/") {
		name := strings.TrimPrefix(r.URL.Path, "/assets/")
		if name == "" || strings.Contains(name, "/") || strings.Contains(name, "..") {
			return fail("الصفحة غير موجودة", 404)
		}
		if !admin {
			for _, private := range []string{"admin.js", "editor.js", "storage.js", "report.js", "report.css"} {
				if name == private {
					return fail("الصفحة غير موجودة", 404)
				}
			}
		}
		path = "web/assets/" + name
	} else {
		return fail("الصفحة غير موجودة", 404)
	}
	data, e := webFiles.ReadFile(path)
	if e != nil {
		return fail("الصفحة غير موجودة", 404)
	}
	ctype := "application/octet-stream"
	switch {
	case strings.HasSuffix(path, ".html"):
		ctype = "text/html; charset=utf-8"
	case strings.HasSuffix(path, ".js"):
		ctype = "text/javascript; charset=utf-8"
	case strings.HasSuffix(path, ".css"):
		ctype = "text/css; charset=utf-8"
	case strings.HasSuffix(path, ".svg"):
		ctype = "image/svg+xml"
	case strings.HasSuffix(path, ".png"):
		ctype = "image/png"
	case strings.HasSuffix(path, ".webmanifest"):
		ctype = "application/manifest+json; charset=utf-8"
	case strings.HasSuffix(path, ".ttf"):
		ctype = "font/ttf"
	}
	send(w, data, ctype)
	return nil
}
func main() {
	port := flag.Int("port", 8765, "student LAN port")
	integrated := flag.Bool("center-integrated", false, "managed center integration")
	studentAddress := flag.String("student-address", "0.0.0.0", "student listener IPv4")
	adminPort := flag.Int("admin-port", 8766, "local admin port")
	dataDir := flag.String("data-dir", "", "data directory")
	parentPID := flag.Int("parent-pid", 0, "desktop launcher process")
	pdfHelper := flag.String("pdf-helper", "", "bundled PDF renderer")
	flag.Parse()
	if (*port == *adminPort && *port != 0) || *port < 0 || *adminPort < 0 || *port > 65535 || *adminPort > 65535 {
		log.Fatal("ports must be distinct")
	}
	if net.ParseIP(*studentAddress) == nil || net.ParseIP(*studentAddress).To4() == nil {
		log.Fatal("student-address must be IPv4")
	}
	if *dataDir == "" {
		home, _ := os.UserHomeDir()
		*dataDir = filepath.Join(home, ".local", "share", "massar-exam-room")
	}
	if e := os.MkdirAll(*dataDir, 0700); e != nil {
		log.Fatal(e)
	}
	unlock, e := dataLock(*dataDir)
	if e != nil {
		log.Fatal(e)
	}
	defer unlock()
	diagnostics := openDiagnosticRecorder(filepath.Join(*dataDir, "diagnostics"))
	defer func() {
		ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
		defer cancel()
		diagnostics.close(ctx)
	}()
	db, e := openDB(filepath.Join(*dataDir, "exams.sqlite3"))
	if e != nil {
		log.Fatal("backend startup failed: ", diagnostics.startupFailed(e))
	}
	defer db.sql.Close()
	stopSleepProtection := startExamSleepProtection(db, func(err error) {
		diagnostics.record(diagnosticEvent{Source: "backend", Kind: "sleep_protection", Category: diagnosticErrorCategory(err)})
	})
	defer stopSleepProtection()
	_, _ = db.sql.Exec(`UPDATE essay_grade_jobs SET state='interrupted',message='توقف البرنامج قبل اكتمال التصحيح',updated_at=? WHERE state='running'`, now())
	g := &grader{db: db, envPath: filepath.Join(*dataDir, ".env")}
	a := &app{db: db, grader: g, adminToken: token(), studentPort: *port, adminPort: *adminPort, addresses: localAddresses, pdfHelper: *pdfHelper, diagnostics: diagnostics, centerIntegrated: *integrated}
	a.whatsapp = newWhatsAppCloud(a)
	if e = a.whatsapp.recoverSends(); e != nil {
		log.Fatal("backend startup failed: ", diagnostics.startupFailed(e))
	}
	studentListener, e := net.Listen("tcp", fmt.Sprintf("%s:%d", *studentAddress, *port))
	if e != nil {
		log.Fatal("backend startup failed: ", diagnostics.startupFailed(e))
	}
	adminListener, e := net.Listen("tcp", fmt.Sprintf("127.0.0.1:%d", *adminPort))
	if e != nil {
		studentListener.Close()
		log.Fatal("backend startup failed: ", diagnostics.startupFailed(e))
	}
	a.studentPort = studentListener.Addr().(*net.TCPAddr).Port
	a.adminPort = adminListener.Addr().(*net.TCPAddr).Port
	if *studentAddress != "0.0.0.0" {
		a.addresses = func() []string { return []string{*studentAddress} }
	}
	if *integrated && runtime.GOOS == "windows" {
		availableAddresses := a.addresses
		a.addresses = func() []string { return centerStudentAddresses(availableAddresses()) }
	}
	_ = db.expire()
	_, _ = db.backup()
	studentServer := a.server(false)
	adminServer := a.server(true)
	go func() {
		if e := studentServer.Serve(studentListener); e != nil && e != http.ErrServerClosed {
			log.Print(e)
		}
	}()
	go func() {
		if e := adminServer.Serve(adminListener); e != nil && e != http.ErrServerClosed {
			log.Print(e)
		}
	}()
	if *integrated {
		access, err := a.centerAdminAccess(M{"enabled": true})
		if err != nil {
			log.Fatal(err)
		}
		json.NewEncoder(os.Stdout).Encode(M{"ready": true, "protocol": 1, "adminPort": a.adminPort, "studentPort": a.studentPort, "adminToken": a.adminToken, "adminUrl": access["adminUrl"]})
	}
	log.Printf("Massar Exam Room Go backend: admin http://127.0.0.1:%d", a.adminPort)
	for _, address := range a.addresses() {
		log.Printf("Students: http://%s:%d", address, a.studentPort)
	}
	ticker := time.NewTicker(time.Second)
	defer ticker.Stop()
	backupAt := time.Now()
	signals := make(chan os.Signal, 1)
	signal.Notify(signals, os.Interrupt, syscall.SIGTERM)
	for {
		select {
		case <-signals:
			ctx, cancel := context.WithTimeout(context.Background(), 45*time.Second)
			_ = studentServer.Shutdown(ctx)
			_ = adminServer.Shutdown(ctx)
			cancel()
			g.mu.Lock()
			if g.cancel != nil {
				g.cancel()
			}
			g.mu.Unlock()
			g.wg.Wait()
			_, _ = db.backup()
			return
		case <-ticker.C:
			if !*integrated && *parentPID > 0 && os.Getppid() != *parentPID {
				select {
				case signals <- os.Interrupt:
				default:
				}
			}
			if e := db.expire(); e != nil {
				diagnostics.record(diagnosticEvent{Source: "backend", Kind: "maintenance", Category: diagnosticErrorCategory(e)})
			}
			if time.Since(backupAt) >= 5*time.Minute {
				if _, err := db.backup(); err != nil {
					diagnostics.record(diagnosticEvent{Source: "backend", Kind: "backup", Category: diagnosticErrorCategory(err)})
				}
				backupAt = time.Now()
			}
		}
	}
}
