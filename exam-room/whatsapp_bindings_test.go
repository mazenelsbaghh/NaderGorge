package main

import (
	"context"
	"fmt"
	"testing"
)

func TestWhatsAppBindingsPersistAndStayScopedToTemplateAccountAndMessageKind(t *testing.T) {
	c, base := centerCloudBatch(t)
	template := parentTemplate()
	body := M{"templateId": "parent", "bindingSchema": templateBindingDigest(template), "recipientKind": "guardian", "reportKind": "followup", "mappings": base["mappings"]}
	if _, err := c.saveTemplateBindings(body); err != nil {
		t.Fatal(err)
	}
	reopened, err := openDB(c.app.db.path)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.sql.Close()
	cloud := newWhatsAppCloud(&app{db: reopened})
	cache, err := cloud.currentTemplates(context.Background())
	if err != nil {
		t.Fatal(err)
	}
	saved := cache["templates"].([]any)[0].(map[string]any)["savedBindings"].([]M)
	if len(saved) != 1 || saved[0]["recipientKind"] != "guardian" || saved[0]["reportKind"] != "followup" {
		t.Fatal("saved mapping was lost after reopen", saved)
	}
	mappings := saved[0]["mappings"].([]any)
	if len(mappings) != 9 || mappings[0].(map[string]any)["source"] != "name" || mappings[7].(map[string]any)["source"] != "maximum" {
		t.Fatal("component mappings changed", mappings)
	}
	// Equal-length variable lists can change meaning when Meta edits the text.
	template["components"].([]any)[1].(M)["text"] = "الطالب {{1}} في {{2}} و{{3}} و{{4}} و{{5}} درجته {{7}} من {{6}} و{{8}}"
	changed := M{"templates": []any{template}, "syncedAt": now(), "phoneNumberId": "123456", "businessAccountId": "654321"}
	if _, err = cloud.app.db.sql.Exec(`UPDATE app_settings SET value=? WHERE key='whatsapp_cloud_templates'`, encode(changed)); err != nil {
		t.Fatal(err)
	}
	cache, err = cloud.currentTemplates(context.Background())
	if err != nil {
		t.Fatal(err)
	}
	if len(cache["templates"].([]any)[0].(map[string]any)["savedBindings"].([]M)) != 0 {
		t.Fatal("changed template inherited old meanings")
	}
	if _, err = cloud.saveTemplateBindings(body); err == nil {
		t.Fatal("stale mapping was accepted")
	}
	body["bindingSchema"] = templateBindingDigest(template)
	if _, err = cloud.saveTemplateBindings(body); err != nil {
		t.Fatal(err)
	}
	changed["businessAccountId"] = "different-account"
	cache, err = cloud.withTemplateBindings(changed)
	if err != nil {
		t.Fatal(err)
	}
	if len(cache["templates"].([]any)[0].(map[string]any)["savedBindings"].([]M)) != 0 {
		t.Fatal("mapping leaked between accounts")
	}
}

func TestWhatsAppChangedTemplateBlocksPreviewWithOldBindingSchema(t *testing.T) {
	c, body := centerCloudBatch(t)
	body["attemptIds"] = []any{"paper"}
	body["bindingSchema"] = "outdated-template"
	if _, err := c.preview(body); err == nil {
		t.Fatal("changed schema must require another review")
	}
	body["bindingSchema"] = templateBindingDigest(parentTemplate())
	preview, err := c.preview(body)
	if err != nil || len(preview["items"].([]M)) != 1 {
		t.Fatal(preview, err)
	}
}

func TestWhatsAppBindingsRejectUnresolvedAndUnknownVariableSources(t *testing.T) {
	c, _ := centerCloudBatch(t)
	for _, source := range []string{"", "privateField", "literal"} {
		t.Run(source, func(t *testing.T) {
			rows := []any{}
			for range 9 {
				rows = append(rows, M{"source": source})
			}
			if _, err := c.saveTemplateBindings(M{"templateId": "parent", "bindingSchema": templateBindingDigest(parentTemplate()), "recipientKind": "guardian", "reportKind": "attendance", "mappings": rows}); err == nil {
				t.Fatal("invalid mapping accepted")
			}
		})
	}
}

func TestCenterPresenceTemplateExcludesAbsentRowsAndDynamicStatusTemplateKeepsTheirTruth(t *testing.T) {
	c, base := centerCloudBatch(t)
	records, ids := []any{}, []any{}
	statuses := []string{"حاضر", "حاضر • تعويض", "غائب", "لم يُسجل حضور"}
	for index, status := range statuses {
		id := []string{"present", "makeup", "absent", "unrecorded"}[index]
		ids = append(ids, id)
		records = append(records, M{"id": id, "name": "طالب تجريبي", "code": id, "phone": "01012345678", "guardianPhone": "01112345678", "lesson": "الأولى", "month": "الثاني", "center": "سنتر التجربة", "time": "18:00", "homework": "لم يعمل", "attendance": status, "score": float64(0), "maximum": float64(12)})
	}
	batch, err := c.saveCenterBatch(M{"title": "تقرير المتابعة", "messageKind": "followup", "records": records})
	if err != nil {
		t.Fatal(err)
	}
	base["centerBatchId"], base["attemptIds"] = batch["batchId"], ids
	preview, err := c.preview(base)
	if err != nil {
		t.Fatal(err)
	}
	items := preview["items"].([]M)
	for index, item := range items {
		if index < 2 && item["fingerprint"] == nil {
			t.Fatal("actual presence excluded", item)
		}
		if index >= 2 && (str(item["error"]) == "" || item["fingerprint"] != nil) {
			t.Fatal("fixed presence claim offered for absent/unrecorded student", item)
		}
	}
	base["attemptId"] = "absent"
	if _, err = c.prepare(base); err == nil {
		t.Fatal("direct send bypassed attendance guard")
	}
	for _, phrase := range []string{"لم يحضر الحصة", "غائب عن المحاضرة"} {
		absence := M{"id": "absence", "name": "absence_followup", "language": "ar", "status": "APPROVED", "category": "UTILITY", "components": []any{M{"type": "BODY", "text": "الطالب {{1}} " + phrase}}}
		cache := M{"templates": []any{absence}, "syncedAt": now(), "phoneNumberId": "123456", "businessAccountId": "654321"}
		if _, err = c.app.db.sql.Exec(`UPDATE app_settings SET value=? WHERE key='whatsapp_cloud_templates'`, encode(cache)); err != nil {
			t.Fatal(err)
		}
		base["templateId"], base["mappings"] = "absence", []any{M{"source": "name"}}
		preview, err = c.preview(base)
		if err != nil {
			t.Fatal(err)
		}
		for index, item := range preview["items"].([]M) {
			if (index == 2) != (item["fingerprint"] != nil) {
				t.Fatal("fixed absence template changed actual attendance", phrase, item)
			}
		}
	}
	dynamic := M{"id": "dynamic", "name": "attendance_followup", "language": "ar", "status": "APPROVED", "category": "UTILITY", "components": []any{M{"type": "BODY", "text": "الطالب {{student_name}}: {{attendance_status}}، درجته {{score}} من {{max_score}}"}}}
	cache := M{"templates": []any{dynamic}, "syncedAt": now(), "phoneNumberId": "123456", "businessAccountId": "654321"}
	if _, err = c.app.db.sql.Exec(`UPDATE app_settings SET value=? WHERE key='whatsapp_cloud_templates'`, encode(cache)); err != nil {
		t.Fatal(err)
	}
	base["templateId"] = "dynamic"
	base["mappings"] = []any{M{"source": "name"}, M{"source": "attendance"}, M{"source": "score"}, M{"source": "maximum"}}
	preview, err = c.preview(base)
	if err != nil {
		t.Fatal(err)
	}
	for index, item := range preview["items"].([]M) {
		parameters := item["parameters"].([]string)
		if parameters[1] != statuses[index] || parameters[2] != "0" || parameters[3] != "12" {
			t.Fatal("actual status or zero score was changed", item)
		}
	}
}

func TestParentTemplateHomeworkUsesAuthoritativeCenterLabelsAsVerbsWithoutInventingMissingWork(t *testing.T) {
	c, base := centerCloudBatch(t)
	labels := []string{"كامل", "ناقص", "لم يعمل", "لم يُراجع", "لم يحدد واجب", "معفى", "حالة مخصصة"}
	phrases := []string{"عمل", "عمل جزءًا من", "لم يعمل", "لم تُسجل حالة", "لم يُحدد", "معفى من", "حالة مخصصة"}
	records, ids := []any{}, []any{}
	for index, label := range labels {
		id := fmt.Sprintf("homework-%d", index)
		ids = append(ids, id)
		records = append(records, M{"id": id, "name": "طالب تجريبي", "code": id, "phone": "01012345678", "guardianPhone": "01112345678", "attendance": "حاضر", "homework": label, "score": float64(0), "maximum": float64(12), "lesson": "الأولى", "month": "الثاني", "center": "سنتر التجربة", "time": "18:00"})
	}
	batch, err := c.saveCenterBatch(M{"title": "متابعة الحصة", "messageKind": "followup", "records": records})
	if err != nil {
		t.Fatal(err)
	}
	base["centerBatchId"], base["attemptIds"] = batch["batchId"], ids
	preview, err := c.preview(base)
	if err != nil {
		t.Fatal(err)
	}
	for index, item := range preview["items"].([]M) {
		parameters := item["parameters"].([]string)
		if parameters[8] != phrases[index] || parameters[6] != "0" || parameters[7] != "12" {
			t.Fatal("homework/grade meaning changed", labels[index], parameters)
		}
		base["attemptId"] = ids[index]
		prepared, err := c.prepare(base)
		if err != nil {
			t.Fatal(err)
		}
		if prepared.parameters[8] != parameters[8] {
			t.Fatal("preview and prepared send disagree")
		}
	}
	for _, label := range labels {
		if actual := templateHomeworkValue(M{"name": "generic_status_template"}, label); actual != label {
			t.Fatal("generic template status changed", label, actual)
		}
	}
}
