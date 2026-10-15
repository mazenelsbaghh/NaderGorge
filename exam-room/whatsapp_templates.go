package main

import (
	"fmt"
	"regexp"
	"strconv"
	"strings"
)

var namedTemplatePosition = regexp.MustCompile(`\{\{\s*([a-z][a-z0-9_]{0,63})\s*\}\}`)
var fixedAttendancePhrase = regexp.MustCompile(`(?:^|\s)حضر\s+(?:المحاضرة|الحصة)(?:\s|$)`)
var parentFixedAttendancePhrase = regexp.MustCompile(`(?:^|\s)حضر\s+\{\{`)
var fixedAbsencePhrase = regexp.MustCompile(`(?:^|\s)(?:لم\s+يحضر|غائب\s+عن)\s+(?:المحاضرة|الحصة)(?:\s|$)`)

func templateHomeworkValue(template M, value string) string {
	if name := str(template["name"]); name != "parent_follow_center" && name != "parent__follow_center" {
		return value
	}
	// The existing parent template appends "الواجب" to variable 8.
	phrases := map[string]string{"كامل": "عمل", "ناقص": "عمل جزءًا من", "لم يُراجع": "لم تُراجع حالة", "لم يحدد واجب": "لم يُحدد", "لم يُحدد واجب": "لم يُحدد", "معفى": "معفى من"}
	if phrase, exists := phrases[strings.TrimSpace(value)]; exists {
		return phrase
	}
	return value
}

func templateAttendanceConflict(template, report M) string {
	fields, centerReport := report["centerFields"].(M)
	if !centerReport {
		return ""
	}
	parts, reason := templateComponents(template)
	if reason != "" {
		return ""
	}
	text := str(parts["BODY"]["text"])
	body := namedTemplatePosition.ReplaceAllString(templatePositionPattern.ReplaceAllString(text, " "), " ")
	attendance := str(fields["attendance"])
	if fixedAbsencePhrase.MatchString(body) {
		if attendance == "غائب" {
			return ""
		}
		return "قالب الغياب لا يطابق حالة الطالب؛ اختر قالب الحضور أو قالبًا يربط حالة الحضور بمتغير"
	}
	knownParent := str(template["name"]) == "parent_follow_center" || str(template["name"]) == "parent__follow_center"
	if !fixedAttendancePhrase.MatchString(body) && !(knownParent && parentFixedAttendancePhrase.MatchString(text)) {
		return ""
	}
	if attendance == "حاضر" || attendance == "معوّض" || attendance == "معوض" || strings.HasPrefix(attendance, "حاضر • ") {
		return ""
	}
	return "قالب الحضور لا يطابق حالة الطالب؛ اختر قالب الغياب أو قالبًا يربط حالة الحضور بمتغير"
}

// Variable numbers are local to a component: HEADER {{1}} and BODY {{1}}
// are two independent values, even when both happen to be the student's name.
func templateBindings(template M) ([]M, string) {
	if str(template["status"]) != "APPROVED" {
		return nil, "القالب غير معتمد بعد"
	}
	if kind := str(template["category"]); kind != "UTILITY" && kind != "MARKETING" {
		return nil, "قالب التحقق لا يصلح لتقارير الطلاب"
	}
	components, reason := templateComponents(template)
	if reason != "" {
		return nil, reason
	}
	if components["BODY"] == nil {
		return nil, "القالب يحتاج نص الرسالة"
	}
	header := components["HEADER"]
	if format := str(header["format"]); header != nil && format != "TEXT" && format != "DOCUMENT" {
		return nil, "رأس القالب يجب أن يكون نصًا أو مستند PDF"
	}
	if strings.Contains(str(components["FOOTER"]["text"]), "{{") {
		return nil, "التذييل لا يقبل متغيرات"
	}
	fields := []M{}
	add := func(scope, text string, index int) string {
		if text == "" {
			return "نص القالب غير صالح"
		}
		matches := templatePositionPattern.FindAllStringSubmatch(text, -1)
		named := namedTemplatePosition.FindAllStringSubmatch(text, -1)
		if len(named) > 0 && len(matches) > 0 {
			return "لا تخلط المتغيرات المسماة والمرقمة في المكون نفسه"
		}
		stripped := namedTemplatePosition.ReplaceAllString(templatePositionPattern.ReplaceAllString(text, ""), "")
		if strings.Contains(stripped, "{{") {
			return "صيغة متغير القالب غير مدعومة"
		}
		keys := []string{}
		if len(named) > 0 {
			seen := map[string]bool{}
			for _, m := range named {
				if !seen[m[1]] {
					keys = append(keys, m[1])
					seen[m[1]] = true
				}
			}
		} else {
			positions, reason := templatePositions(text)
			if reason != "" {
				return reason
			}
			for _, p := range positions {
				keys = append(keys, strconv.Itoa(p))
			}
		}
		if len(keys) > 30 {
			return "عدد متغيرات القالب أكبر من المسموح"
		}
		for _, key := range keys {
			field := M{"scope": scope, "position": key, "key": scope + ":" + key, "suggestedSource": suggestTemplateSource(template, scope, key), "named": len(named) > 0}
			if scope == "button" {
				field["buttonIndex"] = index
				field["key"] = fmt.Sprintf("button:%d:%s", index, key)
			}
			fields = append(fields, field)
		}
		return ""
	}
	if header != nil && str(header["format"]) == "TEXT" {
		if reason = add("header", str(header["text"]), 0); reason != "" {
			return nil, reason
		}
	}
	if reason = add("body", str(components["BODY"]["text"]), 0); reason != "" {
		return nil, reason
	}
	if buttons := components["BUTTONS"]; buttons != nil {
		raw, ok := buttons["buttons"].([]any)
		if !ok {
			return nil, "أزرار القالب غير صالحة"
		}
		for i, value := range raw {
			button, ok := value.(map[string]any)
			if !ok {
				return nil, "أزرار القالب غير صالحة"
			}
			switch str(button["type"]) {
			case "URL":
				if strings.Contains(str(button["url"]), "{{") {
					before := len(fields)
					if reason = add("button", str(button["url"]), i); reason != "" {
						return nil, reason
					}
					if len(fields)-before != 1 {
						return nil, "زر الرابط يدعم متغيرًا واحدًا"
					}
				}
			case "PHONE_NUMBER", "QUICK_REPLY":
				if strings.Contains(encode(button), "{{") {
					return nil, "هذا الزر لا يدعم متغيرًا نصيًا"
				}
			default:
				return nil, "نوع زر القالب غير مدعوم"
			}
		}
	}
	return fields, ""
}

func suggestTemplateSource(template M, scope, key string) string {
	named := map[string]string{"student_name": "name", "name": "name", "student_code": "code", "code": "code", "score": "score", "max_score": "maximum", "maximum": "maximum", "result": "result", "exam_name": "exam", "lesson": "lesson", "month": "month", "center": "center", "group": "group", "group_name": "group", "time": "time", "homework": "homework", "homework_status": "homework", "attendance": "attendance", "attendance_status": "attendance"}
	if source := named[key]; source != "" {
		return source
	}
	if str(template["name"]) == "parent__follow_center" || str(template["name"]) == "parent_follow_center" {
		if scope == "header" && key == "1" {
			return "name"
		}
		if scope == "body" {
			return map[string]string{"1": "name", "2": "lesson", "3": "month", "4": "center", "5": "time", "6": "score", "7": "maximum", "8": "homework"}[key]
		}
	}
	return ""
}

func filledTemplateComponents(template M, parameters []string, mediaID, filename string) ([]M, error) {
	fields, reason := templateBindings(template)
	if reason != "" {
		return nil, fail(reason, 400)
	}
	if len(fields) != len(parameters) {
		return nil, fail("بيانات متغيرات القالب غير مكتملة", 400)
	}
	parts, _ := templateComponents(template)
	components := []M{}
	byKey := map[string]int{}
	if str(parts["HEADER"]["format"]) == "DOCUMENT" {
		if mediaID == "" {
			return nil, fail("هذا القالب يحتاج تقرير PDF؛ اختر قالبًا نصيًا لتقارير السنتر", 400)
		}
		components = append(components, M{"type": "header", "parameters": []M{{"type": "document", "document": M{"id": mediaID, "filename": filename}}}})
	}
	for i, field := range fields {
		scope := str(field["scope"])
		componentKey := scope
		if scope == "button" {
			componentKey = fmt.Sprintf("button:%d", intv(field["buttonIndex"]))
		}
		index, ok := byKey[componentKey]
		if !ok {
			component := M{"type": scope, "parameters": []M{}}
			if scope == "button" {
				component["sub_type"] = "url"
				component["index"] = strconv.Itoa(intv(field["buttonIndex"]))
			}
			index = len(components)
			byKey[componentKey] = index
			components = append(components, component)
		}
		parameter := M{"type": "text", "text": parameters[i]}
		if field["named"] == true {
			parameter["parameter_name"] = field["position"]
		}
		components[index]["parameters"] = append(components[index]["parameters"].([]M), parameter)
	}
	return components, nil
}

func templateUsesDocument(template M) bool {
	parts, _ := templateComponents(template)
	return str(parts["HEADER"]["format"]) == "DOCUMENT"
}
