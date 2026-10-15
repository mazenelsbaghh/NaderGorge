package main

import (
	"bytes"
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"mime/multipart"
	"net/http"
	"net/textproto"
	"net/url"
	"os"
	"path/filepath"
	"regexp"
	"slices"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"
)

type whatsappConfig struct {
	AccessToken       string `json:"accessToken"`
	PhoneNumberID     string `json:"phoneNumberId"`
	BusinessAccountID string `json:"businessAccountId"`
	APIVersion        string `json:"apiVersion"`
	AppSecret         string `json:"appSecret"`
	VerifyToken       string `json:"verifyToken"`
}

type whatsappCloud struct {
	app         *app
	path        string
	client      *http.Client
	baseURL     string
	mu          sync.Mutex
	sendSlots   chan struct{}
	jobsMu      sync.Mutex
	jobsRunning bool
	jobsWG      sync.WaitGroup
	batchGap    time.Duration
}

var metaIDPattern = regexp.MustCompile(`^[0-9]{5,30}$`)
var metaVersionPattern = regexp.MustCompile(`^v[0-9]{2,3}\.0$`)
var templatePositionPattern = regexp.MustCompile(`\{\{\s*([0-9]+)\s*\}\}`)

func newWhatsAppCloud(a *app) *whatsappCloud {
	return &whatsappCloud{app: a, path: filepath.Join(filepath.Dir(a.db.path), "whatsapp-cloud.json"), client: &http.Client{Timeout: 30 * time.Second, CheckRedirect: func(_ *http.Request, _ []*http.Request) error { return http.ErrUseLastResponse }}, baseURL: "https://graph.facebook.com", sendSlots: make(chan struct{}, 5), batchGap: 250 * time.Millisecond}
}

func (c *whatsappCloud) config() (whatsappConfig, error) {
	var config whatsappConfig
	info, err := os.Lstat(c.path)
	if errors.Is(err, os.ErrNotExist) {
		return config, nil
	}
	if err != nil {
		return config, err
	}
	if !info.Mode().IsRegular() || info.Size() > 65536 {
		return config, fail("ملف إعدادات واتساب غير صالح", 500)
	}
	contents, err := os.ReadFile(c.path)
	if err == nil {
		contents, err = unprotectWhatsAppConfig(contents)
		if err == nil {
			err = json.Unmarshal(contents, &config)
		}
	}
	return config, err
}

func (c *whatsappCloud) settings() (M, error) {
	config, err := c.config()
	if err != nil {
		return nil, err
	}
	return M{"configured": config.AccessToken != "" && config.PhoneNumberID != "" && config.BusinessAccountID != "", "tokenConfigured": config.AccessToken != "", "phoneNumberId": config.PhoneNumberID, "businessAccountId": config.BusinessAccountID, "apiVersion": config.APIVersion, "appSecretConfigured": config.AppSecret != "", "verifyToken": config.VerifyToken, "path": c.path}, nil
}

func (c *whatsappCloud) saveConfig(body M) (M, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	config, err := c.config()
	if err != nil {
		return nil, err
	}
	phone, business, version := str(body["phoneNumberId"]), str(body["businessAccountId"]), str(body["apiVersion"])
	if !metaIDPattern.MatchString(phone) || !metaIDPattern.MatchString(business) || phone == business {
		return nil, fail("اكتب Phone Number ID وBusiness Account ID مختلفين من صفحة API Setup", 400)
	}
	if err = validateWhatsAppVersion(version); err != nil {
		return nil, err
	}
	versionChanged := version != config.APIVersion
	changed := phone != config.PhoneNumberID || business != config.BusinessAccountID || str(body["accessToken"]) != ""
	if changed {
		config.AppSecret = ""
	}
	if supplied := str(body["accessToken"]); supplied != "" {
		if len(supplied) < 20 || len(supplied) > 8192 || strings.ContainsAny(supplied, " \r\n\t") {
			return nil, fail("توكن واتساب غير صالح", 400)
		}
		config.AccessToken = supplied
	}
	if config.AccessToken == "" {
		return nil, fail("أدخل توكن واتساب", 400)
	}
	if secret := str(body["appSecret"]); secret != "" {
		if len(secret) < 16 || len(secret) > 256 || strings.ContainsAny(secret, " \r\n\t") {
			return nil, fail("App Secret غير صالح", 400)
		}
		config.AppSecret = secret
	}
	config.PhoneNumberID = phone
	config.BusinessAccountID = business
	config.APIVersion = version
	if config.VerifyToken == "" {
		config.VerifyToken = token()
	}
	if err = writeWhatsAppConfig(c.path, config); err != nil {
		return nil, err
	}
	if changed || versionChanged {
		if _, err = c.app.db.sql.Exec(`DELETE FROM app_settings WHERE key='whatsapp_cloud_templates'`); err != nil {
			return nil, err
		}
	}
	return c.settings()
}

func writePrivateJSON(path string, contents any) error {
	raw, err := json.Marshal(contents)
	if err != nil {
		return err
	}
	return writePrivateBytes(path, raw)
}
func writePrivateBytes(path string, contents []byte) error {
	if err := os.MkdirAll(filepath.Dir(path), 0700); err != nil {
		return err
	}
	file, err := os.CreateTemp(filepath.Dir(path), ".whatsapp-")
	if err != nil {
		return err
	}
	defer os.Remove(file.Name())
	if err = file.Chmod(0600); err == nil {
		_, err = file.Write(contents)
	}
	if err == nil {
		err = file.Sync()
	}
	closeErr := file.Close()
	if err == nil {
		err = closeErr
	}
	if err != nil {
		return err
	}
	return os.Rename(file.Name(), path)
}

type metaCall struct {
	method, path string
	body         io.Reader
	contentType  string
}

func (c *whatsappCloud) metaRequest(ctx context.Context, config whatsappConfig, call metaCall) (M, error) {
	if err := validateWhatsAppVersion(config.APIVersion); err != nil {
		return nil, err
	}
	request, err := http.NewRequestWithContext(ctx, call.method, c.baseURL+"/"+config.APIVersion+"/"+call.path, call.body)
	if err != nil {
		return nil, err
	}
	request.Header.Set("Authorization", "Bearer "+config.AccessToken)
	if call.contentType != "" {
		request.Header.Set("Content-Type", call.contentType)
	}
	response, err := c.client.Do(request)
	if err != nil {
		return nil, fail("تعذر تأكيد استجابة واتساب؛ راجع سجل الإرسال قبل أي محاولة جديدة", 502)
	}
	defer response.Body.Close()
	contents, err := io.ReadAll(io.LimitReader(response.Body, 4*1024*1024+1))
	if err != nil || len(contents) > 4*1024*1024 {
		return nil, fail("استجابة واتساب غير مكتملة", 502)
	}
	var payload M
	if json.Unmarshal(contents, &payload) != nil {
		return nil, fail("استجابة واتساب غير صالحة", 502)
	}
	if response.StatusCode < 200 || response.StatusCode >= 300 {
		if response.StatusCode >= 500 || response.StatusCode == http.StatusRequestTimeout {
			return nil, fail("استجابة واتساب غير مؤكدة؛ راجع السجل قبل إعادة الإرسال", 502)
		}
		providerCode := 0
		if providerError, ok := payload["error"].(map[string]any); ok {
			providerCode = intv(providerError["code"])
		}
		if response.StatusCode == http.StatusTooManyRequests || providerCode == 130429 || providerCode == 131056 || providerCode == 80007 {
			seconds, _ := strconv.Atoi(response.Header.Get("Retry-After"))
			seconds = max(1, min(seconds, 3600))
			return nil, whatsappRateLimit{code: providerCode, seconds: seconds}
		}
		return nil, fail(fmt.Sprintf("واتساب رفض الطلب (كود %d). تحقق من التوكن والقالب وأهلية الرقم", providerCode), 422)
	}
	return payload, nil
}

func (c *whatsappCloud) templates() (M, error) {
	var raw string
	err := c.app.db.sql.QueryRow(`SELECT value FROM app_settings WHERE key='whatsapp_cloud_templates'`).Scan(&raw)
	if errors.Is(err, sql.ErrNoRows) {
		return M{"templates": []any{}, "syncedAt": nil}, nil
	}
	if err != nil {
		return nil, err
	}
	return decode(raw), nil
}

// The caller holds mu, so simultaneous senders share one refresh.
func (c *whatsappCloud) ensureTemplatesLocked(ctx context.Context) (M, error) {
	cache, err := c.templates()
	if err != nil {
		return nil, err
	}
	config, err := c.config()
	if err != nil {
		return nil, err
	}
	if config.AccessToken == "" {
		return cache, nil
	}
	if str(cache["businessAccountId"]) == config.BusinessAccountID && str(cache["phoneNumberId"]) == config.PhoneNumberID && now()-num(cache["syncedAt"]) < 300 {
		return cache, nil
	}
	return c.syncTemplatesLocked(ctx)
}

func (c *whatsappCloud) currentTemplates(ctx context.Context) (M, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	cache, err := c.ensureTemplatesLocked(ctx)
	if err == nil {
		return c.withTemplateBindings(cache)
	}
	cache, readErr := c.templates()
	if readErr != nil {
		return nil, readErr
	}
	cache["syncError"] = "تعذر تحديث القوالب؛ تأكد من الإنترنت وبيانات حساب واتساب. سيُعاد التحديث تلقائيًا."
	return c.withTemplateBindings(cache)
}

func (c *whatsappCloud) syncTemplates(ctx context.Context) (M, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.syncTemplatesLocked(ctx)
}

func (c *whatsappCloud) syncTemplatesLocked(ctx context.Context) (M, error) {
	config, err := c.config()
	if err != nil {
		return nil, err
	}
	if config.AccessToken == "" || config.BusinessAccountID == "" {
		return nil, fail("احفظ إعدادات حساب واتساب أولًا", 409)
	}
	if err = c.verifyAccount(ctx, config); err != nil {
		return nil, err
	}
	templates := []any{}
	cursor := ""
	seen := map[string]bool{}
	for page := 0; page < 40; page++ {
		path := config.BusinessAccountID + "/message_templates?fields=id,name,language,category,status,components&limit=100"
		if cursor != "" {
			path += "&after=" + url.QueryEscape(cursor)
		}
		payload, err := c.metaRequest(ctx, config, metaCall{method: "GET", path: path})
		if err != nil {
			return nil, err
		}
		rows, ok := payload["data"].([]any)
		if !ok {
			return nil, fail("قائمة القوالب غير صالحة", 502)
		}
		for _, row := range rows {
			template, ok := row.(map[string]any)
			if !ok {
				return nil, fail("قالب واتساب غير صالح", 502)
			}
			_, reason := reportTemplateFields(template)
			fields, _ := templateBindings(template)
			template["reportFields"] = fields
			template["reportSupported"] = reason == ""
			template["reportReason"] = reason
			templates = append(templates, template)
		}
		paging, _ := payload["paging"].(map[string]any)
		if str(paging["next"]) == "" {
			cached := M{"templates": templates, "syncedAt": now(), "businessAccountId": config.BusinessAccountID, "phoneNumberId": config.PhoneNumberID}
			_, err = c.app.db.sql.Exec(`INSERT INTO app_settings(key,value) VALUES('whatsapp_cloud_templates',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value`, encode(cached))
			return cached, err
		}
		cursors, _ := paging["cursors"].(map[string]any)
		cursor = str(cursors["after"])
		if cursor == "" || seen[cursor] {
			return nil, fail("تعذر إكمال صفحات القوالب", 502)
		}
		seen[cursor] = true
	}
	return nil, fail("عدد صفحات القوالب أكبر من المسموح", 502)
}

func reportTemplateFields(template M) ([]int, string) {
	fields, reason := templateBindings(template)
	if reason != "" {
		return nil, reason
	}
	positions := make([]int, len(fields))
	for i := range fields {
		positions[i] = i + 1
	}
	return positions, ""
}

func templateComponents(template M) (map[string]M, string) {
	rawComponents, ok := template["components"].([]any)
	if !ok {
		return nil, "مكونات القالب غير صالحة"
	}
	components := map[string]M{}
	for _, raw := range rawComponents {
		component, ok := raw.(map[string]any)
		if !ok {
			return nil, "مكونات القالب غير صالحة"
		}
		kind := str(component["type"])
		if !slices.Contains([]string{"HEADER", "BODY", "FOOTER", "BUTTONS"}, kind) {
			return nil, "القالب يحتوي مكونًا غير مدعوم"
		}
		if components[kind] != nil {
			return nil, "مكونات القالب متكررة"
		}
		components[kind] = component
	}
	return components, ""
}

func templatePositions(text string) ([]int, string) {
	if text == "" {
		return nil, "نص القالب غير صالح"
	}
	if strings.Contains(templatePositionPattern.ReplaceAllString(text, ""), "{{") {
		return nil, "المتغيرات المسماة غير مدعومة؛ استخدم {{1}} و{{2}}"
	}
	positions := map[int]bool{}
	for _, match := range templatePositionPattern.FindAllStringSubmatch(text, -1) {
		var position int
		fmt.Sscan(match[1], &position)
		if position < 1 || position > 30 {
			return nil, "رقم متغير القالب غير صالح"
		}
		positions[position] = true
	}
	ordered := []int{}
	for position := range positions {
		ordered = append(ordered, position)
	}
	sort.Ints(ordered)
	for i, position := range ordered {
		if position != i+1 {
			return nil, "متغيرات القالب يجب أن تبدأ من 1 دون فجوات"
		}
	}
	return ordered, ""
}

func (c *whatsappCloud) uploadPDF(ctx context.Context, config whatsappConfig, pdf []byte, name string) (string, error) {
	var body bytes.Buffer
	writer := multipart.NewWriter(&body)
	if err := writer.WriteField("messaging_product", "whatsapp"); err != nil {
		return "", err
	}
	if err := writer.WriteField("type", "application/pdf"); err != nil {
		return "", err
	}
	header := make(textproto.MIMEHeader)
	header.Set("Content-Disposition", fmt.Sprintf(`form-data; name="file"; filename="%s"`, strings.ReplaceAll(name, `"`, "-")))
	header.Set("Content-Type", "application/pdf")
	part, err := writer.CreatePart(header)
	if err != nil {
		return "", err
	}
	if _, err = part.Write(pdf); err != nil {
		return "", err
	}
	if err = writer.Close(); err != nil {
		return "", err
	}
	payload, err := c.metaRequest(ctx, config, metaCall{"POST", config.PhoneNumberID + "/media", &body, writer.FormDataContentType()})
	if err != nil {
		return "", err
	}
	mediaID := str(payload["id"])
	if mediaID == "" {
		return "", fail("واتساب لم يؤكد رفع التقرير", 502)
	}
	return mediaID, nil
}

func (c *whatsappCloud) sendPDF(ctx context.Context, report *preparedWhatsAppReport, mediaID string) (string, error) {
	components, err := filledTemplateComponents(report.template, report.parameters, mediaID, report.filename)
	if err != nil {
		return "", err
	}
	payload, err := c.metaRequest(ctx, report.config, metaCall{"POST", report.config.PhoneNumberID + "/messages", strings.NewReader(encode(M{"messaging_product": "whatsapp", "to": report.recipient, "type": "template", "template": M{"name": report.template["name"], "language": M{"code": report.template["language"]}, "components": components}})), "application/json"})
	if err != nil {
		return "", err
	}
	messages, _ := payload["messages"].([]any)
	if len(messages) == 0 {
		return "", fail("واتساب لم يؤكد قبول الرسالة", 502)
	}
	message, _ := messages[0].(map[string]any)
	messageID := str(message["id"])
	if messageID == "" {
		return "", fail("واتساب لم يؤكد قبول الرسالة", 502)
	}
	return messageID, nil
}

func (c *whatsappCloud) verifyAccount(ctx context.Context, config whatsappConfig) error {
	cursor := ""
	for page := 0; page < 40; page++ {
		path := config.BusinessAccountID + "/phone_numbers?fields=id&limit=100"
		if cursor != "" {
			path += "&after=" + url.QueryEscape(cursor)
		}
		payload, err := c.metaRequest(ctx, config, metaCall{method: "GET", path: path})
		if err != nil {
			return err
		}
		rows, ok := payload["data"].([]any)
		if !ok {
			return fail("تعذر التحقق من أرقام الحساب", 502)
		}
		for _, raw := range rows {
			row, ok := raw.(map[string]any)
			if ok && str(row["id"]) == config.PhoneNumberID {
				return nil
			}
		}
		paging, _ := payload["paging"].(map[string]any)
		if str(paging["next"]) == "" {
			break
		}
		cursors, _ := paging["cursors"].(map[string]any)
		next := str(cursors["after"])
		if next == "" || next == cursor {
			break
		}
		cursor = next
	}
	return fail("رقم الإرسال لا ينتمي إلى Business Account ID؛ راجع صفحة API Setup", 409)
}

type whatsappRateLimit struct{ code, seconds int }

func (e whatsappRateLimit) Error() string {
	return "واتساب طلب الانتظار قبل استكمال الإرسال"
}

// Meta's Graph API versions table retires v20.0 on 2026-09-24.
// Keep the selected version explicit instead of relying on Meta's fallback.
// https://developers.facebook.com/docs/graph-api/changelog/versions/
func validateWhatsAppVersion(version string) error {
	if !metaVersionPattern.MatchString(version) {
		return fail("نسخة Meta API غير صالحة؛ مثال v23.0", 400)
	}
	major, _ := strconv.Atoi(strings.TrimSuffix(strings.TrimPrefix(version, "v"), ".0"))
	if major <= 20 {
		return fail("نسخة Meta API القديمة انتهى دعمها؛ انتهى v20.0 في ٢٤ سبتمبر ٢٠٢٦. اختر نسخة مدعومة مثل v23.0 ثم احفظ الحساب من جديد", 400)
	}
	return nil
}
