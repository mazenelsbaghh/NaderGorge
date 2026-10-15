package main

import (
	"database/sql"
	"errors"
	"math"
)

func catalogMaximum(raw any) (float64, error) {
	maximum, ok := raw.(float64)
	if !ok || maximum <= 0 || maximum > 1000000 || math.IsNaN(maximum) || math.IsInf(maximum, 0) {
		return 0, fail("مجموع امتحان في دليل السنتر غير صالح", 400)
	}
	return maximum, nil
}

func cleanCatalogContext(hostID string, raw any) (M, error) {
	context, ok := raw.(map[string]any)
	if !ok {
		return nil, fail("بيانات دليل الحصص غير صالحة", 400)
	}
	clean := M{}
	for _, field := range []string{"groupId", "sessionId", "activityId"} {
		text, err := textField(context, field, 1, 100)
		if err != nil {
			return nil, err
		}
		clean[field] = text
	}
	for _, field := range []string{"groupLabel", "sessionLabel", "activityName"} {
		text, err := textField(context, field, 1, 250)
		if err != nil {
			return nil, err
		}
		clean[field] = text
	}
	maximum, err := catalogMaximum(context["maxScore"])
	if err != nil {
		return nil, err
	}
	clean["maxScore"] = maximum
	allowed := []float64{maximum}
	if rawScores, exists := context["allowedMaxScores"]; exists {
		scores, ok := rawScores.([]any)
		if !ok || len(scores) > 30 {
			return nil, fail("قائمة مجموع الامتحان غير صالحة", 400)
		}
		for _, rawScore := range scores {
			score, err := catalogMaximum(rawScore)
			if err != nil {
				return nil, err
			}
			if score != maximum {
				allowed = append(allowed, score)
			}
		}
	}
	clean["allowedMaxScores"] = allowed
	clean["id"] = hash(hostID + ":" + str(clean["groupId"]) + ":" + str(clean["sessionId"]) + ":" + str(clean["activityId"]))
	return clean, nil
}

// Only academic destination identifiers and labels enter this cache. Unknown
// properties, rosters and financial fields from a caller are never persisted.
func (d *DB) saveCenterCatalog(p M) (M, error) {
	hostID, err := textField(p, "hostId", 1, 100)
	if err != nil {
		return nil, err
	}
	rawContexts, ok := p["contexts"].([]any)
	if !ok || len(rawContexts) > 1000 {
		return nil, fail("دليل الحصص أكبر من الحد المسموح أو غير صالح", 400)
	}
	contexts := []M{}
	seen := map[string]bool{}
	for _, raw := range rawContexts {
		context, err := cleanCatalogContext(hostID, raw)
		if err != nil {
			return nil, err
		}
		key := str(context["id"])
		if seen[key] {
			return nil, fail("حصة وامتحان مكرران في دليل السنتر", 409)
		}
		seen[key] = true
		contexts = append(contexts, context)
	}
	snapshot := M{"protocol": 1, "hostId": hostID, "updatedAt": now(), "contexts": contexts}
	encoded := encode(snapshot)
	if len(encoded) > 1000000 {
		return nil, fail("دليل الحصص أكبر من الحد المسموح", 413)
	}
	_, err = d.sql.Exec(`INSERT INTO app_settings(key,value) VALUES('center_academic_catalog',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value`, encoded)
	if err != nil {
		return nil, err
	}
	return M{"saved": true, "contextCount": len(contexts), "updatedAt": snapshot["updatedAt"]}, nil
}

func cachedCenterCatalog(q Q) (M, error) {
	var raw string
	err := q.QueryRow(`SELECT value FROM app_settings WHERE key='center_academic_catalog'`).Scan(&raw)
	if errors.Is(err, sql.ErrNoRows) {
		return M{"protocol": 1, "hostId": nil, "updatedAt": nil, "contexts": []any{}}, nil
	}
	if err != nil {
		return nil, err
	}
	return decode(raw), nil
}

func cachedStandaloneTarget(q Q, p M, config M) (M, error) {
	reference := str(p["cachedContextId"])
	if reference == "" {
		return nil, nil
	}
	if _, err := textField(p, "cachedContextId", 1, 100); err != nil {
		return nil, err
	}
	catalog, err := cachedCenterCatalog(q)
	if err != nil {
		return nil, err
	}
	for _, raw := range catalog["contexts"].([]any) {
		context := raw.(map[string]any)
		if context["id"] != reference {
			continue
		}
		maximum, err := roomMaximum(Exam{Config: config})
		if err != nil {
			return nil, err
		}
		compatible := false
		for _, rawScore := range context["allowedMaxScores"].([]any) {
			if num(rawScore) == maximum {
				compatible = true
			}
		}
		if !compatible {
			return nil, fail("مجموع نموذج الامتحان لا يطابق مجموع الامتحان المختار في السنتر", 409)
		}
		target := M{"cachedContextId": reference, "hostId": catalog["hostId"], "maxScore": maximum}
		for _, field := range []string{"groupId", "sessionId", "activityId", "groupLabel", "sessionLabel", "activityName"} {
			target[field] = context[field]
		}
		return target, nil
	}
	return nil, fail("الحصة المختارة لم تعد في دليل السنتر المحفوظ؛ حدّث الاختيار أو افتح قاعة دون ربط", 409)
}

func standaloneRoomTarget(q Q, eid string) (M, error) {
	var raw string
	err := q.QueryRow(`SELECT payload FROM center_standalone_targets WHERE exam_id=?`, eid).Scan(&raw)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return decode(raw), nil
}
