package main

import (
	"strings"
	"testing"
)

// Regression: the approved parent template already contains "الشهر", sometimes
// without a space before its placeholder. Both preview and send use parameters.
func TestCenterMonthCompletesApprovedTemplateWithoutRepeatingItsFixedWord(t *testing.T) {
	for _, scenario := range []struct {
		name, phrase, month, parameter string
	}{
		{"joined", "في الشهر{{3}}", "الشهر الثاني", " الثاني"},
		{"spaced", "في الشهر {{3}}", "الشهر الثاني", "الثاني"},
		{"placeholder_spaces", "في الشهر {{ 3 }}", "الشهر الثاني", "الثاني"},
		{"already_short", "في الشهر{{3}}", "الثاني", " الثاني"},
		{"custom_name", "في الشهر {{3}}", "أكتوبر", "أكتوبر"},
		{"full_label", "في {{3}}", "الشهر الثاني", "الشهر الثاني"},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			c, request := centerCloudBatch(t)
			template := parentTemplate()
			body := template["components"].([]any)[1].(M)
			body["text"] = strings.Replace(str(body["text"]), "في {{3}}", scenario.phrase, 1)
			_, err := c.app.db.sql.Exec(`UPDATE app_settings SET value=? WHERE key='whatsapp_cloud_templates'`, encode(M{"templates": []any{template}, "syncedAt": now(), "phoneNumberId": "123456", "businessAccountId": "654321"}))
			if err != nil {
				t.Fatal(err)
			}
			batch, err := c.saveCenterBatch(M{"title": "متابعة الحصة", "records": []any{M{"id": "student", "name": "طالب تجريبي", "code": "001", "guardianPhone": "01112345678", "lesson": "الحصة 1", "month": scenario.month, "center": "سنتر تجريبي", "time": "18:00", "homework": "كامل", "attendance": "حاضر", "score": float64(0), "maximum": float64(10)}}})
			if err != nil {
				t.Fatal(err)
			}
			request["centerBatchId"], request["attemptIds"] = batch["batchId"], []any{"student"}
			preview, err := c.preview(request)
			if err != nil {
				t.Fatal(err)
			}
			parameters := preview["items"].([]M)[0]["parameters"].([]string)
			if parameters[3] != scenario.parameter || parameters[2] != "الحصة 1" || parameters[8] != "عمل" {
				t.Fatal("approved template parameter mismatch", parameters)
			}
			request["attemptId"] = "student"
			prepared, err := c.prepare(request)
			if err != nil {
				t.Fatal(err)
			}
			if prepared.parameters[3] != parameters[3] {
				t.Fatal("preview and prepared send disagree")
			}
		})
	}
}

func TestMonthCompletionKeepsHeaderBodyNamedAndLiteralBindingsIndependent(t *testing.T) {
	for _, scenario := range []struct {
		name, header, body, source, expectedHeader, expectedBody string
	}{
		{"header_only", "الشهر{{1}}", "في {{1}}", "month", " الثاني", "الشهر الثاني"},
		{"body_only", "{{1}}", "في الشهر {{1}}", "month", "الشهر الثاني", "الثاني"},
		{"named", "{{month}}", "في الشهر{{month}}", "month", "الشهر الثاني", " الثاني"},
		{"literal", "الشهر{{1}}", "في الشهر {{1}}", "literal", "الشهر الثاني", "الشهر الثاني"},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			template := M{"name": "custom_month", "status": "APPROVED", "category": "UTILITY", "components": []any{M{"type": "HEADER", "format": "TEXT", "text": scenario.header}, M{"type": "BODY", "text": scenario.body}}}
			report := M{"attempt": M{}, "exam": M{"config": M{}}, "centerFields": M{"month": "الشهر الثاني"}}
			mappings := []any{M{"source": scenario.source, "text": "الشهر الثاني"}, M{"source": scenario.source, "text": "الشهر الثاني"}}
			parameters, err := reportParameters(report, mappings, 2, template)
			if err != nil {
				t.Fatal(err)
			}
			if parameters[0] != scenario.expectedHeader || parameters[1] != scenario.expectedBody {
				t.Fatal("component-local month bindings changed", parameters)
			}
		})
	}
}
