﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MvcSandbox
{
    public class TestModel : PageModel
    {
        [FromQuery]
        [Required]
        public int Id { get; set; }

        public string Name { get; private set; } = "World";

        public IActionResult OnPost(string name)
        {
            Name = name;
            return View();
        }
    }
}
