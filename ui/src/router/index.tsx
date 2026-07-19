import { createBrowserRouter } from "react-router";

import Login from "@/pages/login/Login";
import Home from "@/pages/home";

const router = createBrowserRouter([
    { path: "/", element: <Home /> },
    {
        path: "/login",
        element: <Login />,
    },
]);

export default router;
