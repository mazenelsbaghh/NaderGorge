package main

import (
	"encoding/json"
	"io"
	"strconv"
	"strings"
)

// These explicit sets mirror the desktop ProblemLog contract. Unknown identifiers
// are never copied through; extending app diagnostics requires a reviewed entry.
var diagnosticOperations = map[string]bool{
	"startup":               true,
	"flutter_error":         true,
	"platform_error":        true,
	"zone_error":            true,
	"store_command":         true,
	"store_load":            true,
	"store_save":            true,
	"store_import":          true,
	"store_export":          true,
	"auth":                  true,
	"appearance":            true,
	"documents":             true,
	"printing":              true,
	"ui_operation":          true,
	"unknown_operation":     true,
	"ui.frame":              true,
	"ui.event_loop":         true,
	"reports.build":         true,
	"reports.search":        true,
	"academic.import.read":  true,
	"academic.import.match": true,
	"academic.import.scope": true,
	"database.migrate":      true,
	"lan.decode":            true,
	"cloud.revision":        true,

	"flutter.framework":          true,
	"flutter.platform":           true,
	"flutter.zone":               true,
	"startup.initialize":         true,
	"startup.open":               true,
	"startup.log_directory":      true,
	"startup.export":             true,
	"database.open":              true,
	"database.repair":            true,
	"database.operation":         true,
	"database.close":             true,
	"auth.sign_in":               true,
	"appearance.storage":         true,
	"ui.notice":                  true,
	"ui.warning":                 true,
	"ui.auth_screen":             true,
	"ui.attendance_workspace":    true,
	"ui.student_editor_dialog":   true,
	"ui.student_transfer_dialog": true,
	"ui.management_widgets":      true,
	"ui.academics_page":          true,
	"ui.closings_page":           true,
	"ui.review_page":             true,
	"ui.card_settings_page":      true,
	"ui.reports_page":            true,
	"ui.cards_page":              true,
	"ui.students_page":           true,
	"ui.corrections_page":        true,
	"ui.sessions_page":           true,
	"ui.backup_page":             true,
	"cloud.upload":               true,
	"cloud.snapshot":             true,
	"cloud.settings":             true,
	"cloud.initialize":           true,
	"cloud.updates":              true,
	"ui.cloud_settings":          true,
	"card_settings":              true,
	"card_payment":               true,
	"debt_settle":                true,
	"ui.student_debt_dialog":     true,
	"card_receipt":               true,
	"setup_admin":                true,
	"staff_create":               true,
	"catalog_save":               true,
	"group_save":                 true,
	"month_save_groups":          true,
	"student_save":               true,
	"student_transfer":           true,
	"student_suspend":            true,
	"student_reactivate":         true,
	"student_discount":           true,
	"student_note":               true,
	"session_save":               true,
	"session_start":              true,
	"sessions_create":            true,
	"package_renew":              true,
	"entry":                      true,
	"attendance_record":          true,
	"session_close":              true,
	"session_reopen":             true,
	"session_cancel":             true,
	"academic_activity_save":     true,
	"academic_save":              true,
	"entry_reverse":              true,
	"payment_cancel":             true,
	"attendance_cancel":          true,
	"entry_correct":              true,
	"absence_present":            true,
	"payment_method_correct":     true,
	"package_refund":             true,
	"closing_reopen":             true,
	"payment_check":              true,
	"payment_uncheck":            true,
	"payment_checks_clear":       true,
	"payment_review_save":        true,
	"session_finalize":           true,
	"installation_admin":         true,
	"backup.create":              true,
	"backup.automatic":           true,
	"backup.before_update":       true,
	"backup.restore":             true,
	"reports.export":             true,
	"diagnostics.copy_path":      true,
	"diagnostics.export":         true,
	"lan.initialize":             true,
	"lan.host_start":             true,
	"lan.host_exit":              true,
	"lan.connection":             true,
	"lan.host_request":           true,
	"lan.host_response":          true,
	"lan.command":                true,
	"lan.switch":                 true,
	"lan.saveCatalog":            true,
	"lan.saveGroup":              true,
	"lan.registerStudent":        true,
	"lan.saveStudent":            true,
	"lan.transferStudent":        true,
	"lan.suspendStudent":         true,
	"lan.reactivateStudent":      true,
	"lan.saveSession":            true,
	"lan.startSession":           true,
	"lan.createGroupSessions":    true,
	"lan.saveAcademicActivity":   true,
	"lan.saveAcademic":           true,
	"lan.saveCardSettings":       true,
	"lan.collectStudentCard":     true,
	"lan.settleDebt":             true,
	"lan.saveMonthForGroups":     true,
	"lan.receiveStudentCard":     true,
	"lan.saveStaff":              true,
	"lan.saveStudentDiscount":    true,
	"lan.saveStudentNote":        true,
	"lan.renewPackage":           true,
	"lan.collectAndAttend":       true,
	"lan.closeSession":           true,
	"lan.reopenSession":          true,
	"lan.cancelSession":          true,
	"lan.reverseEntry":           true,
	"lan.cancelPayment":          true,
	"lan.cancelAttendance":       true,
	"lan.correctEntry":           true,
	"lan.markAbsentPresent":      true,
	"lan.correctPaymentMethod":   true,
	"lan.refundPackage":          true,
	"lan.reopenFinancialClosing": true,
	"lan.checkPayment":           true,
	"lan.uncheckPayment":         true,
	"lan.clearPaymentChecks":     true,
	"lan.savePaymentReview":      true,
	"lan.finalizeSession":        true,
	"lan.login":                  true,
	"lan.sign_in":                true,
	"lan.refresh":                true,
	"lan.logout":                 true,
	"ui.lan_settings_page":       true,
}

var diagnosticTypes = map[string]bool{
	"CenterException":           true,
	"LanConnectionException":    true,
	"LanAuthorizationException": true,
	"DatabaseException":         true,
	"FileSystemException":       true,
	"OSError":                   true,
	"SocketException":           true,
	"HttpException":             true,
	"TimeoutException":          true,
	"FormatException":           true,
	"StateError":                true,
	"RangeError":                true,
	"ArgumentError":             true,
	"TypeError":                 true,
	"AssertionError":            true,
	"UnsupportedError":          true,
	"UnimplementedError":        true,
	"NoSuchMethodError":         true,
	"OtherError":                true,
	"FlutterError":              true,
	"PlatformException":         true,
	"MissingPluginException":    true,
}

var diagnosticFiles = map[string]bool{
	"cloud/cloud_support_controller.dart":          true,
	"cloud/cloud_support_settings.dart":            true,
	"cloud/app_update_controller.dart":             true,
	"application/center_store_support.dart":        true,
	"features/management/cloud_settings_page.dart": true,
	"main.dart":                                          true,
	"application/admin_configuration.dart":               true,
	"application/center_reports.dart":                    true,
	"application/center_store.dart":                      true,
	"application/center_store_debts.dart":                true,
	"application/center_store_students.dart":             true,
	"application/center_store_months.dart":               true,
	"application/center_store_backups.dart":              true,
	"application/center_store_lan.dart":                  true,
	"application/center_store_remote.dart":               true,
	"lan/center_store_host_bridge.dart":                  true,
	"lan/lan_transport.dart":                             true,
	"lan/lan_discovery.dart":                             true,
	"lan/lan_controller.dart":                            true,
	"lan/lan_settings.dart":                              true,
	"lan/lan_host_process.dart":                          true,
	"features/management/lan_settings_page.dart":         true,
	"application/installation_admin.dart":                true,
	"application/session_finance.dart":                   true,
	"application/student_card_reports.dart":              true,
	"data/center_state.dart":                             true,
	"domain/models.dart":                                 true,
	"domain/discount_calculation.dart":                   true,
	"features/attendance/attendance_workspace.dart":      true,
	"features/attendance/closed_session_dialog.dart":     true,
	"features/attendance/entry_confirmation_dialog.dart": true,
	"features/attendance/student_editor_dialog.dart":     true,
	"features/attendance/student_discount_dialog.dart":   true,
	"features/attendance/student_debt_dialog.dart":       true,
	"features/attendance/student_suspension_dialog.dart": true,
	"features/attendance/paid_amount_dialog.dart":        true,
	"features/attendance/paid_amount_fields.dart":        true,
	"features/attendance/student_history_panel.dart":     true,
	"features/auth/auth_screen.dart":                     true,
	"features/cards/student_card_actions.dart":           true,
	"features/management/academics_page.dart":            true,
	"features/management/academic_quick_entry.dart":      true,
	"features/management/backup_page.dart":               true,
	"features/management/card_settings_page.dart":        true,
	"features/management/cards_page.dart":                true,
	"features/management/catalogs_page.dart":             true,
	"features/management/closings_page.dart":             true,
	"features/management/corrections_page.dart":          true,
	"features/management/groups_page.dart":               true,
	"features/management/management_widgets.dart":        true,
	"features/management/management_workspace.dart":      true,
	"features/management/reports_page.dart":              true,
	"features/management/review_page.dart":               true,
	"features/management/sessions_page.dart":             true,
	"features/management/staff_page.dart":                true,
	"features/management/students_page.dart":             true,
	"shared/appearance.dart":                             true,
	"shared/app_build_metadata.dart":                     true,
	"shared/document_service.dart":                       true,
	"shared/formatters.dart":                             true,
	"shared/notice_dialog.dart":                          true,
	"shared/scrollable_dialog.dart":                      true,
	"shared/problem_log.dart":                            true,
	"shared/problem_reporting.dart":                      true,
	"shared/problem_log_health.dart":                     true,
	"shared/theme.dart":                                  true,
}

func diagnosticString(entry map[string]any, key string) string {
	value, _ := entry[key].(string)
	return value
}

func diagnosticInteger(value any) (int64, bool) {
	number, ok := value.(json.Number)
	if !ok {
		return 0, false
	}
	integer, err := strconv.ParseInt(string(number), 10, 64)
	return integer, err == nil && integer >= 0 && integer <= 0xffffff
}

func sanitizedDiagnostics(text string) string {
	var output strings.Builder
	for _, line := range strings.Split(text, "\n") {
		if len(line) == 0 || len(line) > 16*1024 {
			continue
		}
		decoder := json.NewDecoder(strings.NewReader(line))
		decoder.UseNumber()
		var input map[string]any
		if decoder.Decode(&input) != nil {
			continue
		}
		var trailing any
		if decoder.Decode(&trailing) != io.EOF {
			continue
		}
		clean := sanitizedDiagnosticEvent(input)
		if clean == nil {
			continue
		}
		encoded, err := json.Marshal(clean)
		if err != nil || len(encoded) > 16*1024 {
			continue
		}
		// Repeated escape sequences can enlarge JSON. Keep the saved field bounded too.
		if output.Len()+len(encoded)+1 > maximumDiagnosticsBytes {
			break
		}
		output.Write(encoded)
		output.WriteByte('\n')
	}
	return output.String()
}

func sanitizedDiagnosticEvent(input map[string]any) map[string]any {
	schema, ok := diagnosticInteger(input["schema"])
	kind := diagnosticString(input, "kind")
	if !ok || schema != 1 || (kind != "error" && kind != "session" && kind != "performance") {
		return nil
	}
	id, session := diagnosticString(input, "id"), diagnosticString(input, "session")
	eventTime, version := diagnosticString(input, "time"), diagnosticString(input, "version")
	platform := diagnosticString(input, "platform")
	metadata := appMetadata{Version: version, Build: "development", Role: "host", OS: platform}
	if !uuidPattern.MatchString(id) || !uuidPattern.MatchString(session) || !validUTC(eventTime) || !validMetadata(metadata) {
		return nil
	}
	operation := diagnosticString(input, "operation")
	if !diagnosticOperations[operation] {
		operation = "unknown_operation"
	}
	clean := map[string]any{"schema": 1, "kind": kind, "id": id, "session": session, "time": eventTime, "version": version, "platform": platform, "operation": operation}
	build := diagnosticString(input, "build")
	if build == "development" || buildPattern.MatchString(build) {
		clean["build"] = build
	}
	role := diagnosticString(input, "role")
	if role == "host" || role == "client" {
		clean["role"] = role
	}
	if kind == "performance" {
		duration, durationOK := diagnosticBoundedInteger(input["durationUs"], 86400000000)
		budget, budgetOK := diagnosticBoundedInteger(input["budgetMs"], 86400000)
		outcome := diagnosticString(input, "outcome")
		if !durationOK || !budgetOK || budget == 0 || (outcome != "completed" && outcome != "failed") {
			return nil
		}
		clean["durationUs"], clean["budgetMs"], clean["outcome"] = duration, budget, outcome
		clean["phasesUs"] = diagnosticMetrics(input["phasesUs"], diagnosticPhases)
		clean["counts"] = diagnosticMetrics(input["counts"], diagnosticCounts)
	}
	if kind == "error" {
		chain, ok := input["errors"].([]any)
		if !ok || len(chain) == 0 {
			return nil
		}
		errors := []map[string]any{}
		for index, value := range chain {
			if index >= 4 {
				break
			}
			error, ok := value.(map[string]any)
			if !ok {
				continue
			}
			errorType := diagnosticString(error, "type")
			if !diagnosticTypes[errorType] {
				errorType = "OtherError"
			}
			cleanError := map[string]any{"type": errorType}
			if code, ok := diagnosticInteger(error["code"]); ok {
				cleanError["code"] = code
			}
			errors = append(errors, cleanError)
		}
		if len(errors) == 0 {
			return nil
		}
		clean["errors"] = errors
		frames := []map[string]any{}
		inputFrames, _ := input["frames"].([]any)
		for index, value := range inputFrames {
			if index >= 16 {
				break
			}
			frame, ok := value.(map[string]any)
			if !ok {
				continue
			}
			file := diagnosticString(frame, "file")
			frameNumber, frameOK := diagnosticInteger(frame["frame"])
			line, lineOK := diagnosticInteger(frame["line"])
			column, columnOK := diagnosticInteger(frame["column"])
			if diagnosticFiles[file] && frameOK && lineOK && columnOK {
				frames = append(frames, map[string]any{"file": file, "frame": frameNumber, "line": line, "column": column})
			}
		}
		clean["frames"] = frames
	}
	return clean
}

// Only fixed labels and bounded integers survive either upload or retrieval.
var diagnosticPhases = map[string]bool{
	"queue":    true,
	"copy":     true,
	"work":     true,
	"validate": true,
	"encode":   true,
	"sqlite":   true,
	"notify":   true,
	"capture":  true,
	"write":    true,
	"prune":    true,
	"connect":  true,
	"response": true,
	"receive":  true,
	"decode":   true,
	"apply":    true,
	"build":    true,
	"raster":   true,
	"match":    true,
	"scope":    true,
	"export":   true,
	"hash":     true,
}
var diagnosticCounts = map[string]bool{
	"students":    true,
	"attendances": true,
	"payments":    true,
	"closings":    true,
	"rows":        true,
	"bytes":       true,
	"sections":    true,
	"records":     true,
	"full":        true,
	"delta":       true,
	"repeats":     true,
}

func diagnosticBoundedInteger(value any, maximum int64) (int64, bool) {
	number, ok := value.(json.Number)
	if !ok {
		return 0, false
	}
	integer, err := strconv.ParseInt(string(number), 10, 64)
	return integer, err == nil && integer >= 0 && integer <= maximum
}
func diagnosticMetrics(value any, allowed map[string]bool) map[string]int64 {
	input, _ := value.(map[string]any)
	clean := map[string]int64{}
	for key := range allowed {
		if integer, ok := diagnosticBoundedInteger(input[key], 86400000000); ok {
			clean[key] = integer
		}
	}
	return clean
}
