export interface StaffDataChangedPayload {
  scopes: string[];
}

const blockedEditingRoutes = [
  /\/new(\/|$)/,
  /\/edit(\/|$)/,
  /\/add-question(\/|$)/,
];

const scopeRoutes: Record<string, RegExp[]> = {
  users: [
    /^\/admin\/(users|students|assistants|admins|teachers)(\/|$)/,
    /^\/teacher\/students(\/|$)/,
    /^\/assistant\/dashboard(\/|$)/,
  ],
  content: [/^\/admin\/content(\/|$)/, /^\/teacher\/packages(\/|$)/, /^\/teacher$/],
  subjects: [/^\/admin\/subjects(\/|$)/, /^\/admin\/teachers(\/|$)/, /^\/teacher\/packages(\/|$)/],
  comments: [/^\/admin\/content(\/|$)/],
  community: [/^\/admin\/community(\/|$)/],
  forms: [/^\/admin\/forms(\/|$)/],
  codes: [/^\/admin\/codes(\/|$)/, /^\/teacher\/codes(\/|$)/, /^\/(admin|teacher)\/finance(\/|$)/],
  'watch-requests': [/^\/admin\/(watch-requests|overrides|users)(\/|$)/],
  assessments: [
    /^\/admin\/questions(\/|$)/,
    /^\/admin\/content\/exams(\/|$)/,
    /^\/teacher\/(exams|essays|activity)(\/|$)/,
    /^\/teacher$/,
  ],
  settings: [/^\/admin\/settings(\/|$)/, /^\/admin$/],
  operations: [/^\/admin\/operations(\/|$)/, /^\/assistant\/(tasks|dashboard)(\/|$)/],
  hr: [
    /^\/admin\/(hr|assistants|finance)(\/|$)/,
    /^\/assistant\/(attendance|vacations|dashboard)(\/|$)/,
  ],
  crm: [/^\/admin\/crm(\/|$)/, /^\/assistant\/crm(\/|$)/],
  media: [/^\/admin\/media(\/|$)/],
  finance: [/^\/admin\/finance(\/|$)/, /^\/teacher\/finance(\/|$)/, /^\/teacher$/],
  reports: [/^\/admin\/reports(\/|$)/],
  notifications: [/^\/assistant\/(notifications|dashboard)(\/|$)/],
  activity: [/^\/teacher\/activity(\/|$)/, /^\/admin\/reports(\/|$)/, /^\/teacher$/],
  balance: [/^\/admin\/(finance|users)(\/|$)/, /^\/teacher\/finance(\/|$)/],
  gamification: [/^\/admin\/users(\/|$)/, /^\/teacher\/activity(\/|$)/],
  ai: [/^\/admin\/(ai-monitor|content)(\/|$)/, /^\/teacher\/packages(\/|$)/],
};

export function shouldRefreshStaffRoute(pathname: string, scopes: string[]): boolean {
  if (blockedEditingRoutes.some((route) => route.test(pathname))) {
    return false;
  }

  return scopes.some((scope) => scopeRoutes[scope]?.some((route) => route.test(pathname)));
}
