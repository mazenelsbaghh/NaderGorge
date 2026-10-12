package main

import (
	"crypto/rand"
	"math/big"
)

func validateQuestionSelection(config M, questions []M) (M, error) {
	_, hasMCQ := config["mcqCount"]
	_, hasEssay := config["essayCount"]
	if !hasMCQ && !hasEssay {
		count, err := validateDrawCount(config, "questionCount", questions)
		if err != nil {
			return nil, err
		}
		if count == 0 {
			return nil, fail("اختر سؤالًا واحدًا على الأقل لكل طالب", 400)
		}
		return M{"questionCount": count}, nil
	}
	selection := M{}
	total := 0
	for _, kind := range []string{"mcq", "essay"} {
		pool := []M{}
		for _, question := range questions {
			if question["kind"] == kind {
				pool = append(pool, question)
			}
		}
		count, err := validateDrawCount(config, kind+"Count", pool)
		if err != nil {
			return nil, err
		}
		selection[kind+"Count"] = count
		total += count
	}
	if total == 0 {
		return nil, fail("اختر سؤالًا واحدًا على الأقل لكل طالب", 400)
	}
	selection["questionCount"] = total
	return selection, nil
}

func validateDrawCount(config M, field string, pool []M) (int, error) {
	count, err := integerField(config, field, 0, len(pool))
	if err != nil {
		labels := map[string]string{"mcqCount": "الاختياري", "essayCount": "المقالي", "questionCount": "الأسئلة"}
		return 0, fail("عدد "+labels[field]+" لازم يكون عددًا صحيحًا من صفر لعدد الأسئلة المتاحة", 400)
	}
	if count > 0 && count < len(pool) {
		for _, question := range pool[1:] {
			if question["points"] != pool[0]["points"] {
				return 0, fail("للسحب العشوائي، اجعل درجات أسئلة البنك المختار متساوية داخل كل نوع لضمان تساوي المجموع", 400)
			}
		}
	}
	return count, nil
}

func shuffledQuestions(questions []any) ([]any, error) {
	shuffled := append([]any{}, questions...)
	for i := len(shuffled) - 1; i > 0; i-- {
		choice, err := rand.Int(rand.Reader, big.NewInt(int64(i+1)))
		if err != nil {
			return nil, err
		}
		j := int(choice.Int64())
		shuffled[i], shuffled[j] = shuffled[j], shuffled[i]
	}
	return shuffled, nil
}

func selectQuestionIDs(config M) ([]string, error) {
	questions := config["questions"].([]any)
	count := intv(config["questionCount"])
	ordered := questions
	if config["shuffle"] == true || count < len(questions) {
		var err error
		ordered, err = shuffledQuestions(questions)
		if err != nil {
			return nil, err
		}
	}
	selected := []string{}
	_, byKind := config["mcqCount"]
	remaining := map[string]int{"mcq": intv(config["mcqCount"]), "essay": intv(config["essayCount"])}
	for _, raw := range ordered {
		question := raw.(map[string]any)
		kind := str(question["kind"])
		if byKind && remaining[kind] == 0 {
			continue
		}
		if len(selected) == count {
			break
		}
		selected = append(selected, str(question["id"]))
		remaining[kind]--
	}
	if byKind && config["shuffle"] != true {
		return bankOrderedIDs(questions, selected), nil
	}
	return selected, nil
}

func bankOrderedIDs(questions []any, selected []string) []string {
	included := map[string]bool{}
	for _, questionID := range selected {
		included[questionID] = true
	}
	ordered := []string{}
	for _, raw := range questions {
		questionID := str(raw.(map[string]any)["id"])
		if included[questionID] {
			ordered = append(ordered, questionID)
		}
	}
	return ordered
}
