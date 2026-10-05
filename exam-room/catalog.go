package main

import (
	"database/sql"
	"errors"
	"strings"
)

func (d *DB) catalog() (M, error) {
	result := M{}
	queries := []struct{ key, query string }{
		{"grades", `SELECT id,name,created_at FROM school_grades ORDER BY created_at,id`},
		{"centers", `SELECT id,grade_id,name,created_at FROM centers ORDER BY created_at,id`},
		{"groups", `SELECT id,center_id,name,created_at FROM study_groups ORDER BY created_at,id`},
		{"lessons", `SELECT id,group_id,name,scheduled_at,created_at FROM lessons ORDER BY created_at,id`},
	}
	for _, item := range queries {
		rows, err := d.sql.Query(item.query)
		if err != nil {
			return nil, err
		}
		entries := []M{}
		for rows.Next() {
			entry := M{}
			var id, parent, name string
			var created float64
			var scheduled sql.NullFloat64
			switch item.key {
			case "grades":
				err = rows.Scan(&id, &name, &created)
			case "centers", "groups":
				err = rows.Scan(&id, &parent, &name, &created)
			case "lessons":
				err = rows.Scan(&id, &parent, &name, &scheduled, &created)
			}
			if err != nil {
				break
			}
			entry["id"], entry["name"], entry["createdAt"] = id, name, created
			switch item.key {
			case "centers":
				entry["gradeId"] = parent
			case "groups":
				entry["centerId"] = parent
			case "lessons":
				entry["groupId"], entry["scheduledAt"] = parent, nullableFloat(scheduled)
			}
			entries = append(entries, entry)
		}
		if err == nil {
			err = rows.Err()
		}
		rows.Close()
		if err != nil {
			return nil, err
		}
		result[item.key] = entries
	}
	return result, nil
}

func catalogTable(kind string) (table, parentColumn, parentTable, parentKey string, ok bool) {
	switch kind {
	case "grades":
		return "school_grades", "", "", "", true
	case "centers":
		return "centers", "grade_id", "school_grades", "gradeId", true
	case "groups":
		return "study_groups", "center_id", "centers", "centerId", true
	case "lessons":
		return "lessons", "group_id", "study_groups", "groupId", true
	}
	return "", "", "", "", false
}

func (d *DB) createCatalogItem(kind string, p M) (M, error) {
	table, parentColumn, parentTable, parentKey, ok := catalogTable(kind)
	if !ok {
		return nil, fail("نوع السجل غير موجود", 404)
	}
	name, err := textField(p, "name", 1, 150)
	if err != nil {
		return nil, err
	}
	parent := ""
	if parentColumn != "" {
		parent, err = textField(p, parentKey, 1, 100)
		if err != nil {
			return nil, err
		}
		var exists int
		err = d.sql.QueryRow(`SELECT 1 FROM `+parentTable+` WHERE id=?`, parent).Scan(&exists)
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fail("اختر المستوى السابق أولًا", 400)
		}
		if err != nil {
			return nil, err
		}
	}
	var scheduled any
	if kind == "lessons" && p["scheduledAt"] != nil && p["scheduledAt"] != "" {
		value, valid := p["scheduledAt"].(float64)
		if !valid || value < 0 {
			return nil, fail("موعد الحصة غير صالح", 400)
		}
		scheduled = value
	}
	itemID := id()
	err = d.tx(func(q *sql.Tx) error {
		var insert string
		var args []any
		switch kind {
		case "grades":
			insert, args = `INSERT INTO school_grades(id,name,created_at) VALUES(?,?,?)`, []any{itemID, name, now()}
		case "lessons":
			insert, args = `INSERT INTO lessons(id,group_id,name,scheduled_at,created_at) VALUES(?,?,?,?,?)`, []any{itemID, parent, name, scheduled, now()}
		default:
			insert, args = `INSERT INTO `+table+`(id,`+parentColumn+`,name,created_at) VALUES(?,?,?,?)`, []any{itemID, parent, name, now()}
		}
		if _, err := q.Exec(insert, args...); err != nil {
			if strings.Contains(err.Error(), "UNIQUE constraint") {
				return fail("الاسم مسجل بالفعل في نفس المستوى", 409)
			}
			return err
		}
		audit(q, "catalog-created", itemID)
		return nil
	})
	return M{"id": itemID}, err
}

func (d *DB) renameCatalogItem(kind, itemID string, p M) (M, error) {
	table, _, _, _, ok := catalogTable(kind)
	if !ok {
		return nil, fail("نوع السجل غير موجود", 404)
	}
	name, err := textField(p, "name", 1, 150)
	if err != nil {
		return nil, err
	}
	err = d.tx(func(q *sql.Tx) error {
		result, err := q.Exec(`UPDATE `+table+` SET name=? WHERE id=?`, name, itemID)
		if err != nil {
			if strings.Contains(err.Error(), "UNIQUE constraint") {
				return fail("الاسم مسجل بالفعل في نفس المستوى", 409)
			}
			return err
		}
		count, _ := result.RowsAffected()
		if count == 0 {
			return fail("السجل غير موجود", 404)
		}
		audit(q, "catalog-renamed", itemID)
		return nil
	})
	return M{"id": itemID}, err
}

func (d *DB) templates() (M, error) {
	rows, err := d.sql.Query(`SELECT id,config,created_at,updated_at FROM exam_templates ORDER BY updated_at DESC,id`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	items := []M{}
	for rows.Next() {
		var itemID, config string
		var created, updated float64
		if err = rows.Scan(&itemID, &config, &created, &updated); err != nil {
			return nil, err
		}
		items = append(items, M{"id": itemID, "config": decode(config), "createdAt": created, "updatedAt": updated})
	}
	return M{"templates": items}, rows.Err()
}

func (d *DB) saveTemplate(templateID string, p M) (M, error) {
	config, err := validateExam(p)
	if err != nil {
		return nil, err
	}
	creating := templateID == ""
	if creating {
		templateID = id()
	}
	err = d.tx(func(q *sql.Tx) error {
		if creating {
			_, err = q.Exec(`INSERT INTO exam_templates(id,config,created_at,updated_at) VALUES(?,?,?,?)`, templateID, encode(config), now(), now())
		} else {
			var result sql.Result
			result, err = q.Exec(`UPDATE exam_templates SET config=?,updated_at=? WHERE id=?`, encode(config), now(), templateID)
			if err == nil {
				var count int64
				count, err = result.RowsAffected()
				if err == nil && count == 0 {
					return fail("الامتحان غير موجود", 404)
				}
			}
		}
		if err == nil {
			audit(q, "template-saved", templateID)
		}
		return err
	})
	return M{"id": templateID}, err
}

func (d *DB) createTemplate(p M) (M, error) {
	return d.saveTemplate("", p)
}

func (d *DB) openRoom(lessonID, templateID string) (M, error) {
	if lessonID == "" || templateID == "" {
		return nil, fail("اختر الحصة واسم الامتحان", 400)
	}
	var config string
	if err := d.sql.QueryRow(`SELECT config FROM exam_templates WHERE id=?`, templateID).Scan(&config); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fail("الامتحان غير موجود", 404)
		}
		return nil, err
	}
	var exists int
	if err := d.sql.QueryRow(`SELECT 1 FROM lessons WHERE id=?`, lessonID).Scan(&exists); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, fail("الحصة غير موجودة", 404)
		}
		return nil, err
	}
	runID := id()
	err := d.tx(func(q *sql.Tx) error {
		if err := allowAnotherRoom(q); err != nil {
			return err
		}
		if _, err := q.Exec(`INSERT INTO exams(id,config,state,created_at,template_id,lesson_id) VALUES(?,?,'waiting',?,?,?)`, runID, config, now(), templateID, lessonID); err != nil {
			return err
		}
		audit(q, "room-opened", runID)
		return nil
	})
	if err == nil {
		_, _ = d.backup()
	}
	return M{"id": runID}, err
}

func (d *DB) whatsappSettings() (M, error) {
	result := M{"senderPhone": "", "templateName": "", "templateBody": "", "enabled": false}
	for key, field := range map[string]string{"whatsapp_sender": "senderPhone", "whatsapp_template_name": "templateName", "whatsapp_template_body": "templateBody"} {
		var value string
		err := d.sql.QueryRow(`SELECT value FROM app_settings WHERE key=?`, key).Scan(&value)
		if err != nil && !errors.Is(err, sql.ErrNoRows) {
			return nil, err
		}
		result[field] = value
	}
	return result, nil
}

func (d *DB) saveWhatsAppSettings(p M) (M, error) {
	sender := str(p["senderPhone"])
	if sender != "" {
		var err error
		sender, err = normalizePhone(sender)
		if err != nil {
			return nil, err
		}
	}
	name, body := str(p["templateName"]), str(p["templateBody"])
	if len([]rune(name)) > 200 || len([]rune(body)) > 4000 {
		return nil, fail("بيانات القالب أطول من المسموح", 400)
	}
	tx, err := d.sql.Begin()
	if err != nil {
		return nil, err
	}
	defer tx.Rollback()
	for key, value := range map[string]string{"whatsapp_sender": sender, "whatsapp_template_name": name, "whatsapp_template_body": body} {
		if _, err = tx.Exec(`INSERT INTO app_settings(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value`, key, value); err != nil {
			return nil, err
		}
	}
	if err = tx.Commit(); err != nil {
		return nil, err
	}
	audit(d.sql, "whatsapp-settings", "template")
	return d.whatsappSettings()
}
