package main

import (
	"database/sql"
	"errors"
	"strings"
)

func multipleRooms(q Q) (bool, error) {
	var value string
	err := q.QueryRow(`SELECT value FROM app_settings WHERE key='multipleRooms'`).Scan(&value)
	if errors.Is(err, sql.ErrNoRows) {
		return false, nil
	}
	return value == "true", err
}
func syncRoomIndex(q Q, enabled bool) error {
	statement := `CREATE UNIQUE INDEX IF NOT EXISTS one_live_exam ON exams ((1)) WHERE state IN ('waiting','running')`
	if enabled {
		statement = `DROP INDEX IF EXISTS one_live_exam`
	}
	_, err := q.Exec(statement)
	return err
}
func (d *DB) roomSettings() (M, error) {
	enabled, err := multipleRooms(d.sql)
	return M{"multipleRooms": enabled}, err
}
func (d *DB) saveRoomSettings(p M) (M, error) {
	enabled, err := boolField(p, "multipleRooms")
	if err != nil {
		return nil, err
	}
	err = d.tx(func(q *sql.Tx) error {
		if !enabled {
			var count int
			if err := q.QueryRow(`SELECT COUNT(*) FROM exams WHERE state IN ('waiting','running')`).Scan(&count); err != nil {
				return err
			}
			if count > 1 {
				return fail("أنه القاعات الإضافية قبل إلغاء خيار تشغيل أكثر من قاعة", 409)
			}
		}
		if err := syncRoomIndex(q, enabled); err != nil {
			return err
		}
		value := "false"
		if enabled {
			value = "true"
		}
		_, err := q.Exec(`INSERT INTO app_settings(key,value) VALUES('multipleRooms',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value`, value)
		if err == nil {
			audit(q, "multiple-rooms:"+value, "settings")
		}
		return err
	})
	return M{"multipleRooms": enabled}, err
}
func allowAnotherRoom(q Q) error {
	enabled, err := multipleRooms(q)
	if err != nil {
		return err
	}
	if enabled {
		return nil
	}
	var count int
	if err = q.QueryRow(`SELECT COUNT(*) FROM exams WHERE state IN ('waiting','running')`).Scan(&count); err != nil {
		return err
	}
	if count > 0 {
		return fail("في قاعة مفتوحة بالفعل. فعّل تشغيل أكثر من قاعة أو أنهِ القاعة الحالية", 409)
	}
	return nil
}
func activeRooms(q Q) ([]M, error) {
	rows, err := q.Query(`SELECT e.id,e.config,e.state,COALESCE(c.name,''),COALESCE(g.name,''),COALESCE(l.name,'') FROM exams e LEFT JOIN lessons l ON l.id=e.lesson_id LEFT JOIN study_groups g ON g.id=l.group_id LEFT JOIN centers c ON c.id=g.center_id WHERE e.state IN ('waiting','running') ORDER BY e.created_at,e.id`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	rooms := []M{}
	for rows.Next() {
		var id, config, state, center, group, lesson string
		if err = rows.Scan(&id, &config, &state, &center, &group, &lesson); err != nil {
			return nil, err
		}
		cfg := decode(config)
		parts := []string{}
		for _, v := range []string{center, group, lesson, str(cfg["title"])} {
			if v != "" {
				parts = append(parts, v)
			}
		}
		rooms = append(rooms, M{"id": id, "title": cfg["title"], "label": strings.Join(parts, " · "), "state": state, "allowLate": cfg["allowLate"]})
	}
	return rooms, rows.Err()
}
