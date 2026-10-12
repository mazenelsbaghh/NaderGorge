package main

import (
	"crypto/sha256"
	"encoding/base64"
	"io"
	"net/http"
	"strconv"
)

const unavailableStyle = `body{margin:0;background:#f5f7fb;color:#142440;font:18px/1.8 system-ui,sans-serif}main{max-width:34rem;margin:10vh auto;padding:1.5rem}h1{font-size:1.6rem;line-height:1.4}a{display:inline-block;background:#142440;color:#fff;text-decoration:none;padding:.6rem 1.2rem;border-radius:.5rem}small{display:block;margin-top:1.5rem;color:#465570}`

const unavailablePage = `<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>تعذر الاتصال بخدمة الامتحان — مسار</title><style>` + unavailableStyle + `</style></head><body><main><h1>تعذر الاتصال بخدمة الامتحان</h1><p>وصلت لبوابة مسار، لكن خدمة الامتحان على الماك لم ترد. خليك متصل بشبكة الامتحان واطلب من المشرف التأكد أن البرنامج يعمل.</p><p>بعد رجوع الخدمة، اضغط إعادة المحاولة.</p><a href="/">إعادة المحاولة</a><small>لو بدأت الامتحان بالفعل، كمّل في نفس النافذة ولا تمسح بيانات المتصفح. الزر يفتح نفس رابط البوابة.</small></main></body></html>`

func writePortalUnavailable(writer http.ResponseWriter, request *http.Request) {
	// The fallback must render even while the backend and all its assets are
	// unavailable. A hash permits only this fixed style, with no executable script.
	digest := sha256.Sum256([]byte(unavailableStyle))
	writer.Header().Set("Content-Type", "text/html; charset=utf-8")
	writer.Header().Set("Content-Length", strconv.Itoa(len(unavailablePage)))
	writer.Header().Set("Content-Security-Policy", "default-src 'none'; style-src 'sha256-"+
		base64.StdEncoding.EncodeToString(digest[:])+"'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'")
	writer.WriteHeader(http.StatusBadGateway)
	if request.Method != http.MethodHead {
		io.WriteString(writer, unavailablePage)
	}
}
