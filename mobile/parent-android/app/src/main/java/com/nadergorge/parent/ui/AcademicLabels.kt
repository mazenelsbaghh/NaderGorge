package com.nadergorge.parent.ui

object AcademicLabels {
    private val stageLabels = mapOf(
        "Primary" to "المرحلة الابتدائية",
        "Preparatory" to "المرحلة الإعدادية",
        "Secondary" to "المرحلة الثانوية",
        "Baccalaureate" to "مرحلة البكالوريا",
        "Azhari" to "التعليم الأزهري",
        "American" to "النظام الأمريكي"
    )

    private val gradeLabels = mapOf(
        "FirstSecondary" to "الصف الأول الثانوي",
        "SecondSecondary" to "الصف الثاني الثانوي",
        "SecondaryGrade3" to "الصف الثالث الثانوي",
        "ThirdSecondary" to "الصف الثالث الثانوي",
        "FirstBaccalaureate" to "الصف الأول بكالوريا",
        "SecondBaccalaureate" to "الصف الثاني بكالوريا",
        "PrimaryGrade1" to "الصف الأول الابتدائي",
        "PrimaryGrade2" to "الصف الثاني الابتدائي",
        "PrimaryGrade3" to "الصف الثالث الابتدائي",
        "PrimaryGrade4" to "الصف الرابع الابتدائي",
        "PrimaryGrade5" to "الصف الخامس الابتدائي",
        "PrimaryGrade6" to "الصف السادس الابتدائي",
        "PrepGrade1" to "الصف الأول الإعدادي",
        "PrepGrade2" to "الصف الثاني الإعدادي",
        "PrepGrade3" to "الصف الثالث الإعدادي",
        "AzhariPrimary1" to "الأول الابتدائي الأزهري",
        "AzhariPrimary2" to "الثاني الابتدائي الأزهري",
        "AzhariPrimary3" to "الثالث الابتدائي الأزهري",
        "AzhariPrimary4" to "الرابع الابتدائي الأزهري",
        "AzhariPrimary5" to "الخامس الابتدائي الأزهري",
        "AzhariPrimary6" to "السادس الابتدائي الأزهري",
        "AzhariPrep1" to "الأول الإعدادي الأزهري",
        "AzhariPrep2" to "الثاني الإعدادي الأزهري",
        "AzhariPrep3" to "الثالث الإعدادي الأزهري",
        "AzhariSecondary1" to "الأول الثانوي الأزهري",
        "AzhariSecondary2" to "الثاني الثانوي الأزهري",
        "AzhariSecondary3" to "الثالث الثانوي الأزهري",
        "AmericanGrade1" to "Grade 1",
        "AmericanGrade2" to "Grade 2",
        "AmericanGrade3" to "Grade 3",
        "AmericanGrade4" to "Grade 4",
        "AmericanGrade5" to "Grade 5",
        "AmericanGrade6" to "Grade 6",
        "AmericanGrade7" to "Grade 7",
        "AmericanGrade8" to "Grade 8",
        "AmericanGrade9" to "Grade 9",
        "AmericanGrade10" to "Grade 10",
        "AmericanGrade11" to "Grade 11",
        "AmericanGrade12" to "Grade 12"
    )

    private val trackLabels = mapOf(
        "Arts" to "أدبي",
        "Science" to "علمي",
        "MedicineAndLifeSciences" to "الطب وعلوم الحياة",
        "EngineeringAndComputerScience" to "الهندسة وعلوم الحاسب",
        "Business" to "قطاع الأعمال",
        "ArtsAndHumanities" to "الآداب والفنون"
    )

    fun grade(value: String?): String = normalize(value, gradeLabels, "الصف الدراسي")

    fun stage(value: String?): String {
        val raw = value?.trim().orEmpty()
        if (raw.isBlank()) return "المرحلة الدراسية"

        stageLabels[raw]?.let { return it }

        return when {
            raw.contains("Baccalaureate", ignoreCase = true) || raw.contains("بكالوريا") -> "مرحلة البكالوريا"
            raw.contains("Secondary", ignoreCase = true) || raw.contains("ثانوي") -> "المرحلة الثانوية"
            raw.contains("Prep", ignoreCase = true) || raw.contains("إعدادي") -> "المرحلة الإعدادية"
            raw.contains("Primary", ignoreCase = true) || raw.contains("ابتدائي") -> "المرحلة الابتدائية"
            raw.contains("Azhari", ignoreCase = true) || raw.contains("أزهري") -> "التعليم الأزهري"
            raw.contains("American", ignoreCase = true) || raw.contains("Grade") -> "النظام الأمريكي"
            else -> raw
        }
    }

    fun track(value: String?): String = normalize(value, trackLabels, "الشعبة")

    private fun normalize(value: String?, labels: Map<String, String>, fallback: String): String {
        val raw = value?.trim().orEmpty()
        if (raw.isBlank()) return fallback
        labels[raw]?.let { return it }
        return raw.replace(Regex("([a-z])([A-Z])"), "$1 $2")
    }
}
