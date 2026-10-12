package main

import (
	"database/sql"
	"errors"
)

// Center staff select the authoritative group/lesson in the desktop app. A
// separate exam catalog is unnecessary; only the reusable question template is
// copied into this room. Creation and binding become visible together.
func (d *DB) openCenterRoom(p M) (M, error) {
	templateID, err := textField(p, "templateId", 1, 100)
	if err != nil {
		return nil, err
	}
	request := M{}
	for key, value := range p {
		request[key] = value
	}
	request["examId"] = id()
	binding, roster, err := parseCenterBinding(request)
	if err != nil {
		return nil, err
	}
	err = d.tx(func(q *sql.Tx) error {
		if err := allowRoomDuringNetworkMaintenance(q); err != nil {
			return err
		}
		// A lost response may be retried without creating another live room.
		var existingID, existingTemplate string
		var existingMaximum float64
		err := q.QueryRow(`SELECT e.id,COALESCE(e.template_id,''),b.max_score FROM exams e JOIN center_bindings b ON b.exam_id=e.id WHERE e.state IN('waiting','running') AND b.group_id=? AND b.session_id=? AND b.activity_id=? LIMIT 1`, binding.GroupID, binding.SessionID, binding.ActivityID).Scan(&existingID, &existingTemplate, &existingMaximum)
		if err == nil {
			if existingTemplate != templateID || existingMaximum != binding.MaxScore {
				return fail("توجد قاعة مفتوحة لهذا الامتحان في الحصة؛ راجع القاعة الحالية أولًا", 409)
			}
			binding.ExamID = existingID
			return writeCenterBinding(q, binding, roster)
		}
		if !errors.Is(err, sql.ErrNoRows) {
			return err
		}
		if err = allowAnotherRoom(q); err != nil {
			return err
		}
		var raw string
		if err = q.QueryRow(`SELECT config FROM exam_templates WHERE id=?`, templateID).Scan(&raw); errors.Is(err, sql.ErrNoRows) {
			return fail("الامتحان غير موجود في المكتبة", 404)
		} else if err != nil {
			return err
		}
		config, err := validateExam(decode(raw))
		if err != nil {
			return err
		}
		if _, err = q.Exec(`INSERT INTO exams(id,config,state,created_at,template_id) VALUES(?,?,'waiting',?,?)`, binding.ExamID, encode(config), now(), templateID); err != nil {
			return err
		}
		if err = writeCenterBinding(q, binding, roster); err != nil {
			return err
		}
		audit(q, "center-room-opened", binding.ExamID)
		return nil
	})
	if err != nil {
		return nil, err
	}
	return M{"id": binding.ExamID, "examId": binding.ExamID}, nil
}

func (d *DB) centerTemplates() ([]M, error) {
	rows, err := d.sql.Query(`SELECT id,config FROM exam_templates ORDER BY updated_at DESC,id`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	templates := []M{}
	for rows.Next() {
		var templateID, raw string
		if err = rows.Scan(&templateID, &raw); err != nil {
			return nil, err
		}
		config := decode(raw)
		chosen, err := selectQuestionIDs(config)
		if err != nil {
			return nil, err
		}
		maximum := adminAttempt(Exam{Config: config}, Attempt{QuestionIDs: encode(chosen)})["maximum"]
		templates = append(templates, M{"id": templateID, "title": config["title"], "maxScore": maximum})
	}
	return templates, rows.Err()
}
